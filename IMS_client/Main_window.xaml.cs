using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Net.Sockets;
using System.Globalization;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using SIPLib;
using log4net;
using SIPLib.src.SIP;

namespace IMS_client
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Main_window : Window,SIPApp
    {
        #region Global_Variables

        SIPStack sip_stack;
        SIPApp app;
        Preferences settings;
        Address_Book address_book;

        Debug_window my_debug_window;
        IM_window my_im_window;

        XDMS_handler xdms_handler;
        Presence_Handler presence_handler;
        IM_Handler im_handler;
        Multimedia_Handler media_handler;
        Call_Handler call_handler;
        private static ILog _log = LogManager.GetLogger(typeof(SIPApp));

        MediaPlayer sound_player = new MediaPlayer();

        static Random r = new Random();


        bool main_window_is_closed;

        #endregion

        #region Global_Methods
        public static T TryFindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
            {
                return parent;
            }
            else
            {
                //use recursion to proceed with next level
                return TryFindParent<T>(parentObject);
            }
        }
        #endregion

        #region SIP_APP
        #endregion



        public Main_window()
        {

            InitializeComponent();

            ImageSource source = this.Icon;

            this.Closed += new EventHandler(Main_window_Closed);
            my_debug_window = new Debug_window();
            my_debug_window.Closing += new System.ComponentModel.CancelEventHandler(my_debug_window_Closing);

            my_im_window = new IM_window();
            my_im_window.Closing += new System.ComponentModel.CancelEventHandler(my_im_window_Closing);

            this.Loaded += new RoutedEventHandler(Main_window_Loaded);

        }

        void Main_window_Loaded(object sender, RoutedEventArgs e)
        {

            Load_Settings();
            Create_Media_Handler();

            if (settings.xdms_enabled)
            {
                Create_XDMS_Handler();
            }

            Create_Stack();
            if (settings.presence_enabled)
            {
                Create_Presence_Handler();
            }

            Create_IM_Handler();

            Create_Call_Handler();
            Load_Address_Book();

        }

        void stack_Error_Event(object sender, StackErrorEventArgs e)
        {
            MessageBox.Show("Generic Error: " + e.exception.ToString());
        }

        void stack_Raw_Sent_Event(object sender, RawEventArgs event_holder)
        {
            Add_RAW_Message_Handler message_handler = new Add_RAW_Message_Handler(my_debug_window.Add_RAW_Message);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, event_holder.data);
        }

        void stack_Raw_Recv_Event(object sender, RawEventArgs event_holder)
        {
            Add_RAW_Message_Handler message_handler = new Add_RAW_Message_Handler(my_debug_window.Add_RAW_Message);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, event_holder.data);
        }

        private void Load_Address_Book()
        {
            if (settings.xdms_enabled)
            {
                address_book = Retrieve_Address_Book(xdms_handler);
            }
            if (address_book == null)
            {
                try
                {
                    XDocument x_doc = XDocument.Load("Resources\\address_book.xml");
                    address_book = Load_Address_Book_from_Xml(x_doc);
                }
                catch (Exception e)
                {
                }
            }
            if (address_book == null)
            {
                MessageBox.Show("Error with Address Book - creating new one");
                address_book = new Address_Book();
                address_book.entries.Add(new Contact());
            }
            foreach (Contact contact in address_book.entries)
            {
                Add_Status_Item_Handler handler = new Add_Status_Item_Handler(Add_Contact_Status_Item);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, contact);
            }
        }

        void sound_player_MediaEnded(object sender, EventArgs e)
        {
            sound_player.Position = new TimeSpan();
        }

        #region Startup_Methods

        public static TransportInfo createTransport(string listen_ip, int listen_port)
        {
            return new TransportInfo(IPAddress.Parse(listen_ip), listen_port, System.Net.Sockets.ProtocolType.Udp);
        }

        private void Create_Stack()
        {
            string myHost = System.Net.Dns.GetHostName();
            System.Net.IPHostEntry myIPs = System.Net.Dns.GetHostEntry(myHost);
          
            int port = 6789;

            if (settings.ims_use_detected_ip)
            {
                settings.ims_ip_address = get_local_ip();
            }

            while (!CheckPortUsage(settings.ims_ip_address, port))
            {
                port = r.Next(5060, 6000); ;
            }
            settings.ims_port = port;

            TransportInfo local_transport = createTransport(settings.ims_ip_address, port);
            app = new SIPApp(local_transport);
            sip_stack = new SIPStack(app);
            
            // TODO
            //sip_stack.uri.user = "alice";
            sip_stack.proxy_ip = settings.ims_proxy_cscf_hostname;
            sip_stack.proxy_port = settings.ims_proxy_cscf_port;

            app.Raw_Sent_Event += new EventHandler<RawEventArgs>(stack_Raw_Sent_Event);
            app.Raw_Recv_Event += new EventHandler<RawEventArgs>(stack_Raw_Recv_Event);
            app.Request_Recv_Event += new EventHandler<SipMessageEventArgs>(stack_Request_Recv_Event);
            app.Response_Recv_Event += new EventHandler<SipMessageEventArgs>(stack_Response_Recv_Event);
            app.Sip_Sent_Event += new EventHandler<SipMessageEventArgs>(stack_Sip_Sent_Event);
            app.Error_Event += new EventHandler<StackErrorEventArgs>(stack_Error_Event);
            app.Reg_Event += new EventHandler<RegistrationChangedEventArgs>(stack_Reg_Event);
        }

        void stack_Sip_Sent_Event(object sender, SipMessageEventArgs e)
        {
            if (Utils.isRequest(e.message))
            {

                Add_Sip_Request_Message_Handler message_handler = new Add_Sip_Request_Message_Handler(my_debug_window.Add_Sip_Request_Message);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, e.message.method, e.message.ToString());
            }
            else
            {
                Add_Sip_Response_Message_Handler message_handler = new Add_Sip_Response_Message_Handler(my_debug_window.Add_Sip_Response_Message);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, e.message.response_code, e.message.ToString());

            }
        }

        void stack_Reg_Event(object sender, RegistrationChangedEventArgs e)
        {
            string state = e.state;

            Update_Status_Text(state);
            if (state.ToString() == "Registered")
            {
                //TODO check this
                //settings.ims_service_route = "";
                //string[] lines = Regex.Split(e.message.headers["Service-Route"], "\r\n");
                //foreach (string address in lines)
                //{
                //    settings.ims_service_route += address;
                //}

                if (settings.presence_enabled)
                {
                    presence_handler.Publish(settings.ims_public_user_identity, "open", "Available", 3600);
                    Retrieve_Status_Of_Contacts();
                }
            }
        }

        void stack_Response_Recv_Event(object sender, SipMessageEventArgs e)
        {
            Add_Sip_Response_Message_Handler message_handler = new Add_Sip_Response_Message_Handler(my_debug_window.Add_Sip_Response_Message);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, e.message.response_code, e.message.ToString());

            Message response = e.message;

            switch (response.response_code)
            {
                case 180:
                    {

                        break;
                    }
                case 200:
                    {

                        break;
                    }
                case 401:
                    {
                        _log.Error("Transaction layer did not handle registration - APP received  401");
                        //UserAgent ua = new UserAgent(this.stack, null, false);
                        //ua.authenticate(response, transaction);
                        break;
                    }
                default:
                    {
                        _log.Info("Response code of " + response.response_code + " is unhandled ");
                    }
                    break;
            }

            if (response.status_code_type == StatusCodes.Informational)
            {
                if (response.response_code == 100)
                {
                    call_handler.SetState(CallState.Calling);
                }
                else if (response.response_code == 180)
                {
                    call_handler.SetState(CallState.Ringing);
                }
                else if (response.response_code == 182)
                {
                    call_handler.SetState(CallState.Queued);
                }
            }
            else if (response.status_code_type == StatusCodes.Successful)
            {
                //TODO Handle Register
                //if (response.headers["cseq"].ToUpper().Contains("REGISTER"))
                //{
                    
                    //sip_stack.registration.latest_response = response;
                    //sip_stack.service_route = response.headers["Service-Route"];
                    //string temp = sip_stack.registration.latest_request.headers["contact"];
                    //if (temp.Contains("expires"))
                    //{
                        
                    //    int expires = int.Parse(temp.Substring(temp.IndexOf("expires=")+8));
                    //    if (expires > 0)
                    //    {
                    //        sip_stack.registration.current_state = "Registered";
                    //        stack_Reg_Event(this, new RegistrationChangedEventArgs("Registered", response));
                    //    }
                    //    else if (expires == 0)
                    //    {
                    //        sip_stack.registration.current_state = "Deregistered";
                    //        stack_Reg_Event(this, new RegistrationChangedEventArgs("Deregistered", response));
                    //   }

                    //}
                    
                //}
                //TODO Handle INVITE
                //if (response.headers["cseq"].ToUpper().Contains("INVITE"))
                //{
                //    call_handler.process_Response(response);
                //}
            }
            else if (response.status_code_type == StatusCodes.Redirection)
            {
            }
            else if (response.status_code_type == StatusCodes.ClientFailure)
            {
                ProcessClientFailure(response);
            }
            else if (response.status_code_type == StatusCodes.ServerFailure)
            {
            }
            else if (response.status_code_type == StatusCodes.GlobalFailure)
            {
            }
            else if (response.status_code_type == StatusCodes.Unknown)
            {
                MessageBox.Show("Unkown Status Code Type");
            }
        }

        void ProcessClientFailure(Message response)
        {
            //TODO handle client failure
            //if (response.status_code == 401)
            //{
            //    sip_stack.registration.latest_response = response;
            //    sip_stack.registration.auth_header = response.headers["WWW-Authenticate"];
            //    string temp = sip_stack.registration.latest_request.headers["contact"];
            //    string expires = temp.Substring(temp.IndexOf("expires=") + 8);
            //    sip_stack.ReRegister(expires);
            //}
            //else if (response.status_code == 403)
            //{
            //    sip_stack.registration.current_state = "Deregistered";
            //    stack_Reg_Event(this, new RegistrationChangedEventArgs("Deregistered", response));   
            //}
        }

        void stack_Request_Recv_Event(object sender, SipMessageEventArgs e)
        {
            Add_Sip_Request_Message_Handler message_handler = new Add_Sip_Request_Message_Handler(my_debug_window.Add_Sip_Request_Message);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, e.message.method, e.message);
            Message request = e.message;
            switch (request.method.ToUpper())
            {
                case "INVITE":
                    {
                        _log.Info("Received INVITE message");
                        Update_Status_Text("Incoming Call");
                        call_handler.SetState(CallState.Ringing);
                        call_handler.incoming_call = request;
                        call_handler.ua = e.ua;
                        break;
                    }
                case "CANCEL":
                    {
                        call_handler.Cancel_call(message);
                        break;
                    }
                case "ACK":
                    {
                        break;
                    }
                case "BYE":
                    {
                        break;
                    }
                case "MESSAGE":
                    {
                        _log.Info("MESSAGE: " + request.body);
                        im_handler.Process_Message(request);

                        if (this.app.messageUA == null)
                        {
                            this.app.messageUA = new UserAgent(this.sip_stack);
                            this.app.messageUA.localParty = this.app.registerUA.localParty;
                            this.app.messageUA.remoteParty = new Address(request.uri.ToString());
                        }
                        Message m = this.app.messageUA.createResponse(200, "OK");
                        this.app.messageUA.sendResponse(m);
                        break;
                    }
                case "OPTIONS":
                case "REFER":
                case "SUBSCRIBE":
                case "NOTIFY":
                case "PUBLISH":
                case "INFO":
                default:
                    {
                        _log.Info("Request with method " + request.method.ToUpper() + " is unhandled");
                        break;
                    }
            }
            if (request.headers.ContainsKey("event"))
            {
                if (request.first("event").ToString().Contains("presence"))
                {
                    presence_handler.Process_Request(request);
                }
            }
        }

        string get_local_ip()
        {
            string strHostName = "";
            strHostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
            IPAddress[] addr = ipEntry.AddressList;
            for (int i = 0; i < addr.Length; i++)
            {
                if (addr[i].AddressFamily.ToString() == "InterNetwork")
                {
                    if (addr[i].ToString() == "127.0.0.1")
                    {
                        break;
                    }
                    return addr[i].ToString();
                }
            }
            MessageBox.Show("Only detected local loop back network interface!");
            return "127.0.0.1";
        }

        private void Create_XDMS_Handler()
        {
            xdms_handler = new XDMS_handler(settings.xdms_user_name,
                settings.xdms_password,
                settings.xdms_server_name,
                settings.xdms_server_port,
                settings.ims_realm);

            xdms_handler.Request_Log_Event += new EventHandler<HttpRequestEventArgs>(XDMS_Request_Log_Event);
            xdms_handler.Response_Log_Event += new EventHandler<HttpWebResponseEventArgs>(XDMS_Response_Log_Event);
        }

        private void Load_Settings()
        {
            settings = Load_Settings_from_Xml("Resources\\settings.xml");
            if (settings == null)
            {
                settings = new Preferences();
            }
            settings.audiocall_local_port = r.Next(1025, 65535);
            settings.videocall_local_port = r.Next(1025, 65535);
        }

        private void Create_Presence_Handler()
        {
            presence_handler = new Presence_Handler(app, settings);
            presence_handler.Presence_Changed_Event += new EventHandler<Presence_Handler.PresenceChangedArgs>(presence_handler_Presence_Changed_Event);
        }

        private void Create_IM_Handler()
        {
            im_handler = new IM_Handler(app, settings);
            im_handler.Message_Recieved_Event += new EventHandler<IM_Handler.Message_Received_Args>(im_handler_Message_Recieved_Event);
            im_handler.Typing_Message_Recieved_Event += new EventHandler<IM_Handler.Typing_Message_Recieved_Args>(IM_Message_Status_Event);
        }

        private void Create_Media_Handler()
        {
            media_handler = new Multimedia_Handler(settings);
            media_handler.Gst_Log_Event += new EventHandler<GstMessageEventArgs>(Gst_Message_Log_Event);
        }

        private void Create_Call_Handler()
        {
            call_handler = new Call_Handler(app, settings, media_handler);
            call_handler.StateChanged += new EventHandler(call_handler_StateChanged);
        }

        void call_handler_StateChanged(object sender, EventArgs e)
        {
            Call_Handler handler = sender as Call_Handler;
            Update_Status_Text(handler.call_state.ToString());
            if (handler.call_state.ToString() == "Ringing")
            {
                sound_player.Dispatcher.Invoke(
                    System.Windows.Threading.DispatcherPriority.Normal,
                    new Action(
                        delegate()
                        {
                            sound_player.Open(new Uri("Resources/ctu24ringtone.mp3", UriKind.Relative));
                            sound_player.MediaEnded += new EventHandler(sound_player_MediaEnded);
                            sound_player.Play();
                        }));
            }
            else
            {
                sound_player.Dispatcher.Invoke(
                 System.Windows.Threading.DispatcherPriority.Normal,
                 new Action(
                     delegate()
                     {
                         sound_player.Stop();
                     }));
            }
        }


        #endregion

        #region Utilities

        public static bool CheckPortUsage(string ip, int port)
        {
            try
            {
                UdpClient udp_client = new UdpClient(new IPEndPoint(IPAddress.Parse(ip), port));
                udp_client.Close();
                return true;
            }
            catch (SocketException error)
            {
                if (error.SocketErrorCode == SocketError.AddressAlreadyInUse /* check this is the one you get */ )
                    return false;
                throw error;
            }
        }


        
        private void Update_Status_Text(string status)
        {
            Status_Text.Dispatcher.Invoke(
           System.Windows.Threading.DispatcherPriority.Normal,
           new Action(
             delegate()
             {
                 Status_Text.Text = status;
             }));
        }

        #endregion

        #region Address_Book

        private Address_Book Retrieve_Address_Book(XDMS_handler xdms_handler)
        {
            Address_Book temp_address_book = null;
            XDocument xml_document = xdms_handler.Retrieve_File("Resources\\address_book.xml");
            if (xml_document.Root != null)
            {
                temp_address_book = Load_Address_Book_from_Xml(xml_document);
            }
            return temp_address_book;
        }

        private void Save_Address_Book(Address_Book address_book, XDMS_handler xdms_handler)
        {
            string xml = Save_Address_Book_to_Xml(address_book);
            if (settings.xdms_enabled)
            {
                xdms_handler.Store_File("address_book.xml", xml);
            }
        }

        private Address_Book Load_Address_Book_from_Xml(XDocument xml_document)
        {

            Address_Book address_book = null;
            if (xml_document.Root != null)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Address_Book));
                    XmlReader reader = xml_document.CreateReader();
                    address_book = (Address_Book)serializer.Deserialize(reader);
                    reader.Close();
                }
                catch (Exception e)
                {
                    MessageBox.Show("Error in loading address book from xml: " + e.Message);
                    MessageBox.Show("Base Exception: " + e.GetBaseException());
                }
            }
            else
            {
                MessageBox.Show("Address book xml not found");
            }
            if (address_book != null)
            {
                foreach (Contact contact in address_book.entries)
                {
                    contact.Get_Status().display_name = contact.Name;
                }
            }

            return address_book;
        }

        private string Save_Address_Book_to_Xml(Address_Book address_book)
        {
            XElement xe = null;
            string xmlDocumentWithDeclaration = "";
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Address_Book));
                TextWriter writer = new StreamWriter("temp");
                serializer.Serialize(writer, address_book);
                writer.Close();

                XDocument x_doc = XDocument.Load("temp");
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(x_doc.ToString());

                XmlDeclaration xmlDeclaration;
                xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
                XmlElement root = xmlDocument.DocumentElement;
                xmlDocument.InsertBefore(xmlDeclaration, root);
                xmlDocumentWithDeclaration = xmlDocument.InnerXml;
            }
            catch (Exception e)
            {
                MessageBox.Show("Error in saving address book to xml: " + e.Message);
                MessageBox.Show("Base Exception: " + e.GetBaseException());
            }
            Save_Address_Book_To_File("Resources\\address_book.xml", xmlDocumentWithDeclaration);
            return xmlDocumentWithDeclaration;
        }

        private void Save_Address_Book_To_File(string filename, string xml)
        {
            TextWriter writer = new StreamWriter(filename);
            writer.Write(xml);
            writer.Close();
        }

        private void Address_Book_Menu_Click(object sender, RoutedEventArgs e)
        {
            Address_Book_window my_address_book_window = new Address_Book_window(address_book);

            my_address_book_window.Show();
            my_address_book_window.SizeToContent = SizeToContent.Manual;
            my_address_book_window.Closed += new EventHandler(My_Address_Book_Window_Closed);
        }

        #endregion

        #region Log_Events

        void XDMS_Request_Log_Event(object sender, HttpRequestEventArgs e)
        {
            Add_Http_Request_Message_Handler message_handler = new Add_Http_Request_Message_Handler(my_debug_window.Add_Http_Request_Message);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, e);
        }

        void XDMS_Response_Log_Event(object sender, HttpWebResponseEventArgs e)
        {
            Add_Http_Response_Message_Handler message_handler = new Add_Http_Response_Message_Handler(my_debug_window.Add_Http_Response_Message);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, e);
        }

        void Gst_Message_Log_Event(object sender, GstMessageEventArgs e)
        {
            Add_Gst_Message_Handler message_handler = new Add_Gst_Message_Handler(my_debug_window.Add_Gst_Message);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, message_handler, e.type, e.message);
        }

        delegate void Add_Sip_Response_Message_Handler(string Code, string message);
        delegate void Add_Sip_Request_Message_Handler(string method, string message);

        delegate void Add_RAW_Message_Handler(string message);


        delegate void Add_Http_Response_Message_Handler(HttpWebResponseEventArgs response);
        delegate void Add_Http_Request_Message_Handler(HttpRequestEventArgs request);

        delegate void Add_Gst_Message_Handler(string type, string message);


        #endregion

        #region Window_Events

        void Main_window_Closed(object sender, EventArgs e)
        {
            main_window_is_closed = true;
            media_handler.Stop_Loop();
            my_debug_window.Close();
            my_im_window.Close();
            media_handler.video_window.Close();
            Save_Settings_to_Xml("Resources\\settings.xml", settings);
            //TODO Shut down SIP Stack / de register / publish offline etc.
            //if (sip_stack.isRunning)
            //{
            //    if (sip_stack.registration != null)
            //    {
            //        sip_stack.Deregister();
                    
            //        if (settings.presence_enabled && presence_handler != null)
            //        {
            //            presence_handler.Publish(settings.ims_public_user_identity, "closed", "Offline", 3600);
            //        }
            //    }
            //    sip_stack.Stop();
            //}
        }

        void Settings_window_Closed(object sender, EventArgs e)
        {
            Save_Settings_to_Xml("Resources\\settings.xml", settings);
        }

        void My_Address_Book_Window_Closed(object sender, EventArgs e)
        {
            if (address_book != null)
            {
                Save_Address_Book(address_book, xdms_handler);
            }
        }

        void my_debug_window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!main_window_is_closed)
            {
                Debug_window debug_window = sender as Debug_window;
                e.Cancel = true;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
             (DispatcherOperationCallback)(arg => { this.Show_Debug_Log_MenuItem.IsChecked = false; return null; }), null);
            }

        }

        private void Show_Debug_Log_Checked(object sender, RoutedEventArgs e)
        {
            my_debug_window.Show();
        }

        private void Show_Debug_Log_Unchecked(object sender, RoutedEventArgs e)
        {
            my_debug_window.Hide();
        }

        void my_im_window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!main_window_is_closed)
            {
                IM_window im_window = sender as IM_window;
                e.Cancel = true;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
             (DispatcherOperationCallback)(arg => { im_window.Hide(); return null; }), null);
            }

        }


        #endregion

        #region Registration

        private void reg_known_user_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            settings.ims_private_user_identity = mi.Tag.ToString();
            settings.ims_public_user_identity = "sip:" + mi.Tag;
            settings.ims_password = mi.Tag.ToString().Remove(mi.Tag.ToString().IndexOf('@'));
            Register();
        }

        private void Register()
        {
            this.app.Register(settings.ims_private_user_identity.Split('@')[0], settings.ims_password, settings.ims_realm);
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            Register();
        }

        private void Deregister_Click(object sender, RoutedEventArgs e)
        {
            //TODO Handle Deregistration
            //if (sip_stack.registration != null)
            //{
            //    string current_state = sip_stack.registration.current_state;
            //    if (current_state.ToUpper().Equals("REGISTERED") ||
            //        current_state.ToUpper().Equals("REGISTERING"))
            //    {
            //        sip_stack.Deregister();

            //        if (settings.presence_enabled)
            //        {
            //            presence_handler.Publish(settings.ims_public_user_identity, "closed", "Offline", 3600);
            //        }
            //    }
            //}
        }

        #endregion

        #region Presence

        private void Retrieve_Status_Of_Contacts()
        {
            Status_ListBox.Dispatcher.Invoke(
          System.Windows.Threading.DispatcherPriority.Normal,
          new Action(
            delegate()
            {
                Status_ListBox.Items.Clear();
            }));
            foreach (Contact contact in address_book.entries)
            {
                Add_Status_Item_Handler handler = new Add_Status_Item_Handler(Add_Contact_Status_Item);
                Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, contact);
                presence_handler.Subscribe(contact.Sip_URI);
            }
        }

        delegate void Add_Status_Item_Handler(Contact contact);

        private MenuItem Create_Menu_Item(string tag, string title)
        {
            MenuItem menu_item = new MenuItem();

            menu_item.Tag = tag;

            TextBlock txt_block = new TextBlock();
            txt_block.Text = title;
            menu_item.Header = txt_block;
            return menu_item;
        }

        private ContextMenu Create_Contact_Context_Menu(string tag)
        {
            ContextMenu context_menu = new ContextMenu();
            MenuItem temp_menu_item = Create_Menu_Item(tag, "Voice Call");
            temp_menu_item.Click += new RoutedEventHandler(Voice_Call_menu_item_Click);
            context_menu.Items.Add(temp_menu_item);

            temp_menu_item = Create_Menu_Item(tag, "Video Call");
            temp_menu_item.Click += new RoutedEventHandler(Video_Call_menu_item_Click);
            context_menu.Items.Add(temp_menu_item);

            temp_menu_item = Create_Menu_Item(tag, "Send Message");
            temp_menu_item.Click += new RoutedEventHandler(Send_Message_menu_item_Click);
            context_menu.Items.Add(temp_menu_item);
            return context_menu;

        }

        void Voice_Call_menu_item_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            call_handler.Start_Call(mi.Tag.ToString(), false, settings.audiocall_local_port, settings.videocall_local_port);
        }

        void Video_Call_menu_item_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            call_handler.Start_Call(mi.Tag.ToString(), true, settings.audiocall_local_port, settings.videocall_local_port);
        }

        private void Add_Contact_Status_Item(Contact contact)
        {
            try
            {
                StackPanel stack_panel = new StackPanel();
                stack_panel.Orientation = Orientation.Horizontal;
                stack_panel.HorizontalAlignment = HorizontalAlignment.Stretch;
                stack_panel.Background = Brushes.Transparent;
                stack_panel.ContextMenu = Create_Contact_Context_Menu(contact.Sip_URI);

                Image basic = new Image();
                basic.Margin = new Thickness(10);
                Binding my_binding = new Binding("basic");
                my_binding.BindsDirectlyToSource = true;
                my_binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                my_binding.Source = contact.Get_Status();
                my_binding.Converter = new Status_Converter();
                basic.SetBinding(Image.SourceProperty, my_binding);
                basic.Width = 30;

                my_binding = new Binding("basic");
                my_binding.BindsDirectlyToSource = true;
                my_binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                my_binding.Source = contact.Get_Status();
                basic.SetBinding(Image.ToolTipProperty, my_binding);


                TextBlock note = new TextBlock();
                note.HorizontalAlignment = HorizontalAlignment.Center;
                note.VerticalAlignment = VerticalAlignment.Center;
                my_binding = new Binding("note");
                my_binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                my_binding.Source = contact.Get_Status();
                note.SetBinding(TextBlock.ToolTipProperty, my_binding);

                my_binding = new Binding("display_name");
                my_binding.UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged;
                my_binding.Source = contact.Get_Status();
                note.SetBinding(TextBlock.TextProperty, my_binding);



                stack_panel.Children.Add(basic);
                stack_panel.Children.Add(note);
                ListBoxItem lbi = new ListBoxItem();
                lbi.Content = stack_panel;

                Status_ListBox.Items.Add(lbi);
                Status_ListBox.Items.Refresh();
            }
            catch (Exception e)
            {
                MessageBox.Show("Creating Status Item " + e.Message);
            }
        }

        void presence_handler_Presence_Changed_Event(object sender, Presence_Handler.PresenceChangedArgs e)
        {

            bool found_contact = false;
            int index = -1;
            int counter = 0;
            try
            {
                foreach (Contact contact in address_book.entries)
                {
                    if (contact.Sip_URI == e.contact)
                    {
                        Status status = contact.Get_Status();
                        status.basic = e.basis;
                        status.note = e.note;

                        found_contact = true;
                        index = counter;

                    }
                    counter++;
                }
                if (!found_contact)
                {
                    MessageBox.Show("Did not find contact for status update (" + e.contact + ")");
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error in Presence Status Update : " + exc.Message);
            }
        }
        #endregion

        #region IM

        delegate void Add_Msg_To_Conv_Handler(string contact, string message);
        private void Add_Msg_To_Conversation(string contact, string message)
        {
            if (!Message_Tab_Exists(contact))
            {
                Create_Message_Tab(contact);
            }

            foreach (TabItem tab_item in my_im_window.IM_TabControl.Items)
            {
                if (tab_item.Tag.ToString() == contact)
                {
                    DockPanel dock_panel = tab_item.Content as DockPanel;

                    Label status_label = dock_panel.Children[1] as Label;
                    status_label.Content = "Message Recieved";

                    RichTextBox text_box = dock_panel.Children[2] as RichTextBox;
                    FlowDocument flow_doc = text_box.Document;

                    Paragraph para = new Paragraph();
                    Span username = new Span();
                    username.Foreground = Brushes.Red;
                    username.Inlines.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(contact.Substring(4, contact.IndexOf('@') - 4)) + ": ");

                    para.Inlines.Add(username);

                    para.Inlines.Add(message);
                    flow_doc.Blocks.Add(para);

                    //text_box.Foreground = Brushes.Red;
                    //text_box.AppendText(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(contact.Substring(4, contact.IndexOf('@') - 4)) + ":");
                    //text_box.Foreground = Brushes.Black;
                    //text_box.AppendText(message+"\n");
                }
            }
            my_im_window.Show();
        }

        void im_handler_Message_Recieved_Event(object sender, IM_Handler.Message_Received_Args e)
        {
            Add_Msg_To_Conv_Handler handler = new Add_Msg_To_Conv_Handler(Add_Msg_To_Conversation);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, e.contact, e.message);
        }

        delegate void Update_IM_Message_Status_Handler(string contact, string status);
        private void Update_IM_Message_Status(string contact, string status)
        {
            foreach (TabItem tab_item in my_im_window.IM_TabControl.Items)
            {
                if (tab_item.Tag.ToString() == contact)
                {
                    DockPanel dock_panel = tab_item.Content as DockPanel;

                    Label status_label = dock_panel.Children[1] as Label;
                    status_label.Content = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(contact.Substring(4, contact.IndexOf('@') - 4)) + " is typing";
                }
            }
        }

        void IM_Message_Status_Event(object sender, IM_Handler.Typing_Message_Recieved_Args e)
        {
            Update_IM_Message_Status_Handler handler = new Update_IM_Message_Status_Handler(Update_IM_Message_Status);
            Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, e.contact, e.message);
        }

        bool Message_Tab_Exists(string uri)
        {
            foreach (TabItem tab_item in my_im_window.IM_TabControl.Items)
            {
                if (tab_item.Tag.ToString() == uri)
                {
                    return true;
                }
            }
            return false;
        }

        void Create_Message_Tab(string uri)
        {
            TabItem tab_item = new TabItem();
            DockPanel overall_dock_panel = new DockPanel();
            DockPanel send_dock_panel = new DockPanel();
            TextBox text_box = new TextBox();
            text_box.VerticalAlignment = VerticalAlignment.Stretch;
            text_box.VerticalContentAlignment = VerticalAlignment.Center;
            text_box.TextChanged += new TextChangedEventHandler(Send_IM_TextChanged);
            text_box.Tag = uri;

            RichTextBox conversation_box = new RichTextBox();

            FlowDocument conversation_flow_doc = new FlowDocument();
            conversation_box.Document = conversation_flow_doc;

            ImageButton image_button = new ImageButton();
            image_button.ImageOver = "Status_Images/available.png";
            image_button.ImageDown = "Status_Images/Offline.png";
            image_button.ImageNormal = "Status_Images/Unknown.png";
            image_button.Text = "Send";
            image_button.Style = (Style)FindResource("Image_Button_With_text");
            image_button.Width = 60;
            image_button.Height = 30;
            image_button.Click += new RoutedEventHandler(Send_IM_Button_Clicked);
            image_button.Tag = uri;

            conversation_box.VerticalAlignment = VerticalAlignment.Stretch;
            conversation_box.HorizontalAlignment = HorizontalAlignment.Stretch;
            conversation_box.Background = Brushes.White;
            conversation_box.IsReadOnly = true;


            Border border = new Border();
            border.Style = (Style)FindResource("MainBorder");
            border.Child = image_button;

            send_dock_panel.Children.Add(border);
            send_dock_panel.Children.Add(text_box);


            Label status_label = new Label();
            status_label.Content = "";


            DockPanel.SetDock(send_dock_panel, Dock.Bottom);
            DockPanel.SetDock(image_button, Dock.Right);
            DockPanel.SetDock(text_box, Dock.Left);
            DockPanel.SetDock(status_label, Dock.Bottom);
            DockPanel.SetDock(conversation_box, Dock.Top);




            overall_dock_panel.Children.Add(send_dock_panel);
            overall_dock_panel.Children.Add(status_label);
            overall_dock_panel.Children.Add(conversation_box);

            tab_item.Content = overall_dock_panel;
            tab_item.Header = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(uri.Substring(4, uri.IndexOf('@') - 4));

            tab_item.Tag = uri;

            my_im_window.IM_TabControl.Items.Add(tab_item);
        }

        void Send_IM_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox text_box = sender as TextBox;
            if (text_box.Text != "")
            {
                im_handler.Send_Typing_Notice(text_box.Tag.ToString());
            }
        }

        void Send_Message_menu_item_Click(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (!Message_Tab_Exists(mi.Tag.ToString()))
            {
                Create_Message_Tab(mi.Tag.ToString());
            }
            my_im_window.Show();
        }

        void Send_IM_Button_Clicked(object sender, RoutedEventArgs e)
        {
            ImageButton img_button = sender as ImageButton;
            TabItem conversation_tab = null;
            foreach (TabItem tab_item in my_im_window.IM_TabControl.Items)
            {
                if (tab_item.Tag.ToString() == img_button.Tag.ToString())
                {
                    conversation_tab = tab_item;
                }
            }

            DockPanel dock_panel = conversation_tab.Content as DockPanel;
            DockPanel send_panel = dock_panel.Children[0] as DockPanel;
            TextBox text_box = send_panel.Children[1] as TextBox;

            string message = text_box.Text;
            text_box.Text = "";

            im_handler.Send_Message(img_button.Tag.ToString(), message);

            RichTextBox rich_text_box = dock_panel.Children[2] as RichTextBox;
            FlowDocument flow_doc = rich_text_box.Document;

            Paragraph para = new Paragraph();
            Span username = new Span();
            username.Foreground = Brushes.Green;
            username.Inlines.Add(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(settings.ims_public_user_identity.Substring(4, settings.ims_public_user_identity.IndexOf('@') - 4)) + ": ");
            
            para.Inlines.Add(username);

            para.Inlines.Add(message);
            flow_doc.Blocks.Add(para);
            
        }

        #endregion

        #region Settings
        private Preferences Load_Settings_from_Xml(string filename)
        {
            Preferences settings = null;
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Preferences));
                XmlReader reader = XmlReader.Create(filename);
                settings = (Preferences)serializer.Deserialize(reader);
                reader.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show("There was a problem reading the configuration file, settings.xml:" + e.Message);
            }
            return settings;
        }

        private void Save_Settings_to_Xml(string filename, Preferences settings)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Preferences));
                TextWriter writer = new StreamWriter(filename);
                serializer.Serialize(writer, settings);
                writer.Close();
            }
            catch (Exception e)
            {
                MessageBox.Show("Error in saving settings: " + e.Message);
                MessageBox.Show("Base Exception: " + e.GetBaseException());
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            Settings_window my_settings_window = new Settings_window();
            my_settings_window.SizeToContent = SizeToContent.WidthAndHeight;

            TabControl options_tab_control = my_settings_window.Options_tab_control;

            PropertyInfo[] properties = null;
            properties = typeof(Preferences).GetProperties();


            if (settings != null)
            {
                Dictionary<string, int> dict_counter = new Dictionary<string, int>();

                foreach (string section in settings.option_sections)
                {
                    dict_counter.Add(section.ToLower(), 0);
                    TabItem section_tab_item = new TabItem();
                    section_tab_item.Name = section.ToLower();
                    section_tab_item.Header = section;

                    DockPanel dock = new DockPanel();
                    Grid options_grid = new Grid();

                    options_grid.HorizontalAlignment = HorizontalAlignment.Stretch;
                    options_grid.VerticalAlignment = VerticalAlignment.Stretch;

                    ColumnDefinition coldef = new ColumnDefinition();
                    //coldef.Width = GridLength.;
                    options_grid.ColumnDefinitions.Add(coldef);
                    coldef = new ColumnDefinition();
                    //coldef.Width = GridLength.Auto;
                    options_grid.ColumnDefinitions.Add(coldef);
                    dock.Children.Add(options_grid);

                    section_tab_item.Content = dock;
                    options_tab_control.Items.Add(section_tab_item);
                }
                Binding myBinding = null;

                foreach (PropertyInfo prop_info in properties)
                {
                    RowDefinition rowdef = new RowDefinition();
                    //rowdef.Height = GridLength.Auto;
                    string section = prop_info.Name.Remove(prop_info.Name.ToString().IndexOf('_'));
                    Grid options_grid = null;
                    foreach (TabItem tab_item in options_tab_control.Items)
                    {
                        if (tab_item.Name.ToLower() == section)
                        {
                            DockPanel dock = tab_item.Content as DockPanel;
                            options_grid = dock.Children[0] as Grid;
                        }
                    }


                    options_grid.RowDefinitions.Add(rowdef);

                    UIElement new_control = Add_Settings_Item(prop_info.Name);
                    if (new_control != null)
                    {
                        TextBlock name = new TextBlock();
                        string option_name = prop_info.Name.Remove(0, section.Length + 1);
                        option_name = option_name.Replace("_", " ");
                        CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
                        TextInfo textInfo = cultureInfo.TextInfo;
                        name.Text = textInfo.ToTitleCase(option_name);
                        name.Margin = new Thickness(2);
                        //name.HorizontalContentAlignment = HorizontalAlignment.Center;
                        name.HorizontalAlignment = HorizontalAlignment.Center;
                        //name.VerticalContentAlignment = VerticalAlignment.Center;
                        name.VerticalAlignment = VerticalAlignment.Center;

                        options_grid.Children.Add(new_control);
                        options_grid.Children.Add(name);

                        Grid.SetColumn(new_control, 1);
                        Grid.SetRow(new_control, dict_counter[section]);

                        Grid.SetColumn(name, 0);
                        Grid.SetRow(name, dict_counter[section]);
                        dict_counter[section] = dict_counter[section] + 1;
                        if (new_control is ComboBox)
                        {
                            Load_Defaults_ComboBox((ComboBox)new_control, option_name);
                        }
                    }
                }
            }
            my_settings_window.Show();
            my_settings_window.SizeToContent = SizeToContent.Manual;
            my_settings_window.Closed += new EventHandler(Settings_window_Closed);
        }

        private void Load_Defaults_ComboBox(ComboBox new_control,string option_name)
        {
            if (option_name.ToLower().Contains("auth"))
            {
                new_control.Items.Add("MD5");
                new_control.Items.Add("AKAv1-MD5");
            }
        }

        private UIElement Add_Settings_Item(string property_name)
        {
            Binding myBinding = new Binding(property_name);
            myBinding.Source = settings;

            if (settings.setting_item_types.ContainsKey(property_name))
            {
                switch (settings.setting_item_types[property_name])
                {
                    case "checkbox":
                        CheckBox checkbox = new CheckBox();
                        checkbox.SetBinding(CheckBox.IsCheckedProperty, myBinding);
                        checkbox.MaxHeight = 30;
                        checkbox.MaxWidth = 200;
                        checkbox.HorizontalContentAlignment = HorizontalAlignment.Center;
                        checkbox.HorizontalAlignment = HorizontalAlignment.Stretch;
                        checkbox.VerticalContentAlignment = VerticalAlignment.Center;
                        checkbox.VerticalAlignment = VerticalAlignment.Stretch;
                        return checkbox;
                    case "audio_codec_choice":
                    //CheckBox checkbox = new CheckBox();
                    //checkbox.SetBinding(CheckBox.IsCheckedProperty, myBinding);
                    //checkbox.MaxHeight = 30;
                    //checkbox.MaxWidth = 200;
                    //checkbox.HorizontalContentAlignment = HorizontalAlignment.Center;
                    //checkbox.HorizontalAlignment = HorizontalAlignment.Stretch;
                    //checkbox.VerticalContentAlignment = VerticalAlignment.Center;
                    //checkbox.VerticalAlignment = VerticalAlignment.Stretch;
                    //return checkbox;

                    case "hidden":
                        return null;
                    case "combobox":
                        ComboBox combobox = new ComboBox();
                        combobox.SetBinding(ComboBox.TextProperty, myBinding);
                        combobox.MaxHeight = 30;
                        combobox.MaxWidth = 200;
                        combobox.HorizontalContentAlignment = HorizontalAlignment.Center;
                        combobox.HorizontalAlignment = HorizontalAlignment.Stretch;
                        combobox.VerticalContentAlignment = VerticalAlignment.Center;
                        combobox.VerticalAlignment = VerticalAlignment.Stretch;
                        return combobox;
                    default:
                        TextBox textbox = new TextBox();
                        textbox.SetBinding(TextBox.TextProperty, myBinding);
                        textbox.MaxHeight = 30;
                        textbox.MaxWidth = 200;
                        textbox.HorizontalContentAlignment = HorizontalAlignment.Center;
                        textbox.HorizontalAlignment = HorizontalAlignment.Stretch;
                        textbox.VerticalContentAlignment = VerticalAlignment.Center;
                        textbox.VerticalAlignment = VerticalAlignment.Stretch;
                        return textbox;
                }
            }
            else
            {
                TextBox textbox = new TextBox();
                textbox.SetBinding(TextBox.TextProperty, myBinding);
                textbox.MaxHeight = 30;
                textbox.MaxWidth = 200;
                textbox.HorizontalContentAlignment = HorizontalAlignment.Center;
                textbox.HorizontalAlignment = HorizontalAlignment.Stretch;
                textbox.VerticalContentAlignment = VerticalAlignment.Center;
                textbox.VerticalAlignment = VerticalAlignment.Stretch;
                return textbox;
            }
        }

        #endregion

        #region General_Stack
        void my_user_agent_IncomingCall(object sender, Message message)
        {
            //TODO handle incoming call
            //    if (!call_handler.in_call)
            //    {
            //        call_handler.incoming_call = e.Call;
            //        //call_handler.SetState(SIP_UA_CallState.WaitingToAccept);
            //        Update_Status_Text("Incoming Call");
            //        sound_player.Dispatcher.Invoke(
            //            System.Windows.Threading.DispatcherPriority.Normal,
            //            new Action(
            //                delegate()
            //                {
            //                    sound_player.Open(new Uri("Resources/ctu24ringtone.mp3", UriKind.Relative));
            //                    sound_player.MediaEnded += new EventHandler(sound_player_MediaEnded);
            //                    sound_player.Play();
            //                }));
            //    }
            //    else
            //    {

            //    }
        }

        #endregion

        private void Answer_Call_Click(object sender, RoutedEventArgs e)
        {
            Action workAction = delegate
            {
                System.ComponentModel.BackgroundWorker worker = new System.ComponentModel.BackgroundWorker();
                worker.DoWork += delegate
                {
                    call_handler.Receive_Call();
                };
                worker.RunWorkerAsync();
            };
            this.Dispatcher.BeginInvoke(DispatcherPriority.Background, workAction);
            sound_player.Dispatcher.Invoke(
                 System.Windows.Threading.DispatcherPriority.Normal,
                 new Action(
                     delegate()
                     {
                         sound_player.Stop();
                     }));
            call_handler.SetState(CallState.Active);
        }

        private void Cancel_Call_Click(object sender, RoutedEventArgs e)
        {
            //TODO Check Cancel Call
            sound_player.Dispatcher.Invoke(
                 System.Windows.Threading.DispatcherPriority.Normal,
                 new Action(
                     delegate()
                     {
                         sound_player.Stop();
                     }));
            call_handler.Cancel_call(null);
        }

        private void Image_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

    }
}
