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
using SIPLib.SIP;
using SIPLib.Utils;
using log4net;

namespace IMS_client
{
    /// <summary>
    /// Interaction logic for Window1.xaml
    /// </summary>
    public partial class Main_window : Window
    {
        #region Global_Variables

        SIPStack _sipStack;
        SIPApp _app;
        Preferences _settings;
        AddressBook _addressBook;

        readonly Debug_window _myDebugWindow;
        readonly IM_window _myIMWindow;

        XdmsHandler _xdmsHandler;
        PresenceHandler _presenceHandler;
        IMHandler _imHandler;
        MultimediaHandler _mediaHandler;
        CallHandler _callHandler;
        private static readonly ILog Log = LogManager.GetLogger(typeof(SIPApp));

        readonly MediaPlayer _soundPlayer = new MediaPlayer();

        static readonly Random Random = new Random();


        bool _mainWindowIsClosed;

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
            //use recursion to proceed with next level
            return TryFindParent<T>(parentObject);
        }
        #endregion

        #region SIP_APP
        #endregion



        public Main_window()
        {

            InitializeComponent();

            Closed += MainWindowClosed;
            _myDebugWindow = new Debug_window();
            _myDebugWindow.Closing += MyDebugWindowClosing;

            _myIMWindow = new IM_window();
            _myIMWindow.Closing += MyIMWindowClosing;

            Loaded += MainWindowLoaded;

        }

        void MainWindowLoaded(object sender, RoutedEventArgs e)
        {

            LoadSettings();
            Create_Media_Handler();

            if (_settings.xdms_enabled)
            {
                Create_XDMS_Handler();
            }

            CreateStack();
            if (_settings.presence_enabled)
            {
                Create_Presence_Handler();
            }

            Create_IM_Handler();

            Create_Call_Handler();
            LoadAddressBook();

        }

        void stack_Error_Event(object sender, StackErrorEventArgs e)
        {
            MessageBox.Show("Generic Error: " + e.Exception);
        }

        void StackRawSentEvent(object sender, RawEventArgs eventHolder)
        {
            AddRawMessageHandler messageHandler = _myDebugWindow.AddRawMessage;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, eventHolder.Data,eventHolder.Sent);
        }

        void StackRawRecvEvent(object sender, RawEventArgs eventHolder)
        {
           AddRawMessageHandler messageHandler = _myDebugWindow.AddRawMessage;
           Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, eventHolder.Data,eventHolder.Sent);
        }

        private void LoadAddressBook()
        {
            if (_settings.xdms_enabled)
            {
                _addressBook = RetrieveAddressBook(_xdmsHandler);
            }
            if (_addressBook == null)
            {
                try
                {
                    XDocument xDoc = XDocument.Load("Resources\\address_book.xml");
                    _addressBook = LoadAddressBookfromXml(xDoc);
                }
                catch (Exception e)
                {
                }
            }
            if (_addressBook == null)
            {
                MessageBox.Show("Error with Address Book - creating new one");
                _addressBook = new AddressBook();
                _addressBook.Entries.Add(new Contact());
            }
            foreach (Contact contact in _addressBook.Entries)
            {
                AddStatusItemHandler handler = AddContactStatusItem;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, contact);
            }
        }

        void SoundPlayerMediaEnded(object sender, EventArgs e)
        {
            _soundPlayer.Position = new TimeSpan();
        }

        #region Startup_Methods

        public static TransportInfo CreateTransport(string listenIP, int listenPort)
        {
            return new TransportInfo(IPAddress.Parse(listenIP), listenPort, ProtocolType.Udp);
        }

        private void CreateStack()
        {
            string myHost = Dns.GetHostName();
            IPHostEntry myIPs = Dns.GetHostEntry(myHost);
          
            int port = 6789;

            if (_settings.ims_use_detected_ip)
            {
                _settings.ims_ip_address = GetLocalIP();
            }

            while (!CheckPortUsage(_settings.ims_ip_address, port))
            {
                port = Random.Next(5060, 6000); 
            }
            _settings.ims_port = port;

            TransportInfo localTransport = CreateTransport(_settings.ims_ip_address, port);
            _app = new SIPApp(localTransport);
            _sipStack = new SIPStack(_app)
                            {ProxyHost = _settings.ims_proxy_cscf_hostname, ProxyPort = _settings.ims_proxy_cscf_port};

            // TODO
            //sip_stack.uri.user = "alice";


            _app.RawSentEvent += StackRawSentEvent;
            _app.RawRecvEvent += StackRawRecvEvent;
            _app.RequestRecvEvent += StackRequestRecvEvent;
            _app.ResponseRecvEvent += StackResponseRecvEvent;
            _app.SipSentEvent += StackSipSentEvent;
            _app.ErrorEvent += stack_Error_Event;
            _app.RegEvent += StackRegEvent;
        }

        void StackSipSentEvent(object sender, SipMessageEventArgs e)
        {
            if (Helpers.IsRequest(e.Message))
            {

                AddSipRequestMessageHandler messageHandler = _myDebugWindow.AddSipRequestMessage;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, e.Message.Method, e.Message.ToString(),true);
            }
            else
            {
                AddSipResponseMessageHandler messageHandler = _myDebugWindow.AddSipResponseMessage;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, e.Message.ResponseCode, e.Message.ToString(),true);

            }
        }

        void StackRegEvent(object sender, RegistrationChangedEventArgs e)
        {
            string state = e.State;

            UpdateStatusText(state);
            if (state == "Registered")
            {
                //TODO check this
                //settings.ims_service_route = "";
                //string[] lines = Regex.Split(e.message.headers["Service-Route"], "\r\n");
                //foreach (string address in lines)
                //{
                //    settings.ims_service_route += address;
                //}
                if (_settings.presence_enabled)
                {
                    _presenceHandler.Publish(_settings.ims_public_user_identity, "open", "Available", 3600);
                    RetrieveStatusOfContacts();
                }
            }
        }

        void StackResponseRecvEvent(object sender, SipMessageEventArgs e)
        {
            AddSipResponseMessageHandler messageHandler = _myDebugWindow.AddSipResponseMessage;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, e.Message.ResponseCode, e.Message.ToString(),false);

            Message response = e.Message;

            switch (response.ResponseCode)
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
                        Log.Error("Transaction layer did not handle registration - APP received  401");
                        //UserAgent ua = new UserAgent(this.stack, null, false);
                        //ua.authenticate(response, transaction);
                        break;
                    }
                default:
                    {
                        Log.Info("Response code of " + response.ResponseCode + " is unhandled ");
                    }
                    break;
            }

            if (response.StatusCodeType == StatusCodes.Informational)
            {
                if (response.ResponseCode == 100)
                {
                    _callHandler.SetState(CallState.Calling);
                }
                else if (response.ResponseCode == 180)
                {
                    _callHandler.SetState(CallState.Ringing);
                }
                else if (response.ResponseCode == 182)
                {
                    _callHandler.SetState(CallState.Queued);
                }
            }
            else if (response.StatusCodeType == StatusCodes.Successful)
            {
                if (response.First("CSeq").ToString().ToUpper().Contains("REGISTER"))
                {
                    string temp = response.First("Contact").ToString();
                    if (temp.Contains("expires"))
                    {
                        int expires = -1;
                        try
                        {
                            string expire_header = temp.Substring(temp.IndexOf("expires=") + 8);
                            int expireEndIndex = -1;
                            expireEndIndex = expire_header.IndexOf(";");
                            if (expireEndIndex == -1)
                            {
                                expires = int.Parse(expire_header);
                            }
                            else
                            {
                                expires = int.Parse(expire_header.Substring(0, expireEndIndex));
                            }
                            
                        }
                        catch (Exception)
                        {
                            MessageBox.Show("Error finding expire heading on contact header: " + temp);
                        }
                        

                        if (expires > 0)
                        {
                            this._app.RegState = "Registered";
                            StackRegEvent(this, new RegistrationChangedEventArgs("Registered", response));
                        }
                        else if (expires == 0)
                        {
                            this._app.RegState = "Deregistered";
                            StackRegEvent(this, new RegistrationChangedEventArgs("Deregistered", response));
                       }
                        else if (expires < 0)
                        {
                            MessageBox.Show("Error expires is negative: " + expires);
                        }
                    }
                }
                //TODO Handle INVITE
                //if (response.headers["cseq"].ToUpper().Contains("INVITE"))
                //{
                //    call_handler.process_Response(response);
                //}
            }
            else if (response.StatusCodeType == StatusCodes.Redirection)
            {

            }
            else if (response.StatusCodeType == StatusCodes.ClientFailure)
            {
                ProcessClientFailure(response);
            }
            else if (response.StatusCodeType == StatusCodes.ServerFailure)
            {

            }
            else if (response.StatusCodeType == StatusCodes.GlobalFailure)
            {

            }
            else if (response.StatusCodeType == StatusCodes.Unknown)
            {
                if (response.ResponseCode == 503)
                {
                    MessageBox.Show(response.ResponseText);
                }
                else
                {
                    MessageBox.Show("Unkown Status Code Type received");
                }
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

        void StackRequestRecvEvent(object sender, SipMessageEventArgs e)
        {
            AddSipRequestMessageHandler messageHandler = _myDebugWindow.AddSipRequestMessage;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, e.Message.Method, e.Message.ToString(),false);
            Message request = e.Message;
            switch (request.Method.ToUpper())
            {
                case "INVITE":
                    {
                        Log.Info("Received INVITE message");
                        UpdateStatusText("Incoming Call");
                        _callHandler.SetState(CallState.Ringing);
                        _callHandler.IncomingCall = request;
                        _app.Useragents.Add(e.UA);
                        //_callHandler.UA = e.UA;
                        break;
                    }
                case "CANCEL":
                    {
                        _callHandler.CancelCall(request);
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
                        Log.Info("MESSAGE: " + request.Body);
                        _imHandler.ProcessMessage(request);


                        if (e.UA == null)
                        {
                            e.UA = new UserAgent(_sipStack)
                                                 {
                                                     LocalParty = _app.RegisterUA.LocalParty,
                                                     RemoteParty = new Address(request.Uri.ToString())
                                                 };
                        }
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        break;
                    }
                case "OPTIONS":
                case "REFER":
                case "SUBSCRIBE":
                case "NOTIFY":
                    {
                        if (e.UA == null)
                        {
                            e.UA = new UserAgent(_sipStack)
                                                       {
                                                           LocalParty = _app.RegisterUA.LocalParty,
                                                           RemoteParty = new Address(request.Uri.ToString())
                                                       };
                        }
                        Message m = e.UA.CreateResponse(200, "OK");
                        e.UA.SendResponse(m);
                        _presenceHandler.ProcessRequest(request);
                        break;
                    }
                case "PUBLISH":
                case "INFO":
                default:
                    {
                        Log.Info("Request with method " + request.Method.ToUpper() + " is unhandled");
                        break;
                    }
            }
            if (request.Headers.ContainsKey("event"))
            {
                if (request.First("event").ToString().Contains("presence"))
                {
                    _presenceHandler.ProcessRequest(request);
                }
            }
        }

        static string GetLocalIP()
        {
            string strHostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);
            IPAddress[] addr = ipEntry.AddressList;
            foreach (IPAddress t in addr)
            {
                if (t.AddressFamily.ToString() == "InterNetwork")
                {
                    if (t.ToString() == "127.0.0.1")
                    {
                        break;
                    }
                    return t.ToString();
                }
            }
            MessageBox.Show("Only detected local loop back network interface!");
            return "127.0.0.1";
        }

        private void Create_XDMS_Handler()
        {
            _xdmsHandler = new XdmsHandler(_settings.xdms_user_name,
                _settings.xdms_password,
                _settings.xdms_server_name,
                _settings.xdms_server_port,
                _settings.ims_realm);

            _xdmsHandler.RequestLogEvent += XdmsRequestLogEvent;
            _xdmsHandler.ResponseLogEvent += XdmsResponseLogEvent;
        }

        private void LoadSettings()
        {
            _settings = Load_Settings_from_Xml("Resources\\settings.xml") ?? new Preferences();
            _settings.audiocall_local_port = Random.Next(1025, 65535);
            _settings.videocall_local_port = Random.Next(1025, 65535);
        }

        private void Create_Presence_Handler()
        {
            _presenceHandler = new PresenceHandler(_app);
            _presenceHandler.PresenceChangedEvent += PresenceHandlerPresenceChangedEvent;
        }

        private void Create_IM_Handler()
        {
            _imHandler = new IMHandler(_app);
            _imHandler.MessageRecievedEvent += IMHandlerMessageRecievedEvent;
            _imHandler.TypingMessageRecievedEvent += IMMessageStatusEvent;
        }

        private void Create_Media_Handler()
        {
            _mediaHandler = new MultimediaHandler(_settings);
            _mediaHandler.GstLogEvent += GstMessageLogEvent;
        }

        private void Create_Call_Handler()
        {
            _callHandler = new CallHandler(_app, _settings, _mediaHandler,_settings.audiocall_local_port,_settings.videocall_local_port);
            _callHandler.StateChanged += CallHandlerStateChanged;
        }

        void CallHandlerStateChanged(object sender, EventArgs e)
        {
            CallHandler handler = sender as CallHandler;
            if (handler == null) return;
            UpdateStatusText(handler.CallState.ToString());
            if (handler.CallState.ToString() == "Ringing")
            {
                _soundPlayer.Dispatcher.Invoke(
                    DispatcherPriority.Normal,
                    new Action(
                        delegate
                            {
                                _soundPlayer.Open(new Uri("Resources/ctu24ringtone.mp3", UriKind.Relative));
                                _soundPlayer.MediaEnded += SoundPlayerMediaEnded;
                                _soundPlayer.Play();
                            }));
            }
            else
            {
                _soundPlayer.Dispatcher.Invoke(
                    DispatcherPriority.Normal,
                    new Action(
                        () => _soundPlayer.Stop()));
            }
        }


        #endregion

        #region Utilities

        public static bool CheckPortUsage(string ip, int port)
        {
            try
            {
                UdpClient udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(ip), port));
                udpClient.Close();
                return true;
            }
            catch (SocketException error)
            {
                if (error.SocketErrorCode == SocketError.AddressAlreadyInUse /* check this is the one you get */ )
                    return false;
                throw;
            }
        }


        
        private void UpdateStatusText(string status)
        {
            Status_Text.Dispatcher.Invoke(
           DispatcherPriority.Normal,
           new Action(
             delegate {
                 Status_Text.Text = status;
             }));
        }

        #endregion

        #region Address_Book

        private AddressBook RetrieveAddressBook(XdmsHandler xdmsHandler)
        {
            AddressBook tempAddressBook = null;
            XDocument xmlDocument = xdmsHandler.RetrieveFile("Resources\\address_book.xml");
            if (xmlDocument.Root != null)
            {
                tempAddressBook = LoadAddressBookfromXml(xmlDocument);
            }
            return tempAddressBook;
        }

        private void SaveAddressBook(AddressBook addressBook, XdmsHandler xdmsHandler)
        {
            string xml = SaveAddressBookToXml(addressBook);
            if (_settings.xdms_enabled)
            {
                xdmsHandler.StoreFile("address_book.xml", xml);
            }
        }

        private AddressBook LoadAddressBookfromXml(XDocument xmlDocument)
        {

            AddressBook addressBook = null;
            if (xmlDocument.Root != null)
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(AddressBook));
                    XmlReader reader = xmlDocument.CreateReader();
                    addressBook = (AddressBook)serializer.Deserialize(reader);
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
            if (addressBook != null)
            {
                foreach (Contact contact in addressBook.Entries)
                {
                    contact.GetStatus().DisplayName = contact.Name;
                }
            }

            return addressBook;
        }

        private string SaveAddressBookToXml(AddressBook addressBook)
        {
            string xmlDocumentWithDeclaration = "";
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(AddressBook));
                TextWriter writer = new StreamWriter("temp");
                serializer.Serialize(writer, addressBook);
                writer.Close();

                XDocument xDoc = XDocument.Load("temp");
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(xDoc.ToString());

                XmlDeclaration xmlDeclaration = xmlDocument.CreateXmlDeclaration("1.0", "UTF-8", null);
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

        private void AddressBookMenuClick(object sender, RoutedEventArgs e)
        {
            AddressBookWindow myAddressBookWindow = new AddressBookWindow(_addressBook);

            myAddressBookWindow.Show();
            myAddressBookWindow.SizeToContent = SizeToContent.Manual;
            myAddressBookWindow.Closed += MyAddressBookWindowClosed;
        }

        #endregion

        #region Log_Events

        void XdmsRequestLogEvent(object sender, HttpRequestEventArgs e)
        {
            AddHttpRequestMessageHandler messageHandler = _myDebugWindow.AddHttpRequestMessage;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, e);
        }

        void XdmsResponseLogEvent(object sender, HttpWebResponseEventArgs e)
        {
            AddHttpResponseMessageHandler messageHandler = _myDebugWindow.AddHttpResponseMessage;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, e);
        }

        void GstMessageLogEvent(object sender, GstMessageEventArgs e)
        {
            AddGstMessageHandler messageHandler = _myDebugWindow.AddGstMessage;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, messageHandler, e.Type, e.Message);
        }

        delegate void AddSipResponseMessageHandler(int code, string message, bool sent);
        delegate void AddSipRequestMessageHandler(string method, string message,bool sent);

        delegate void AddRawMessageHandler(string message,bool sent);


        delegate void AddHttpResponseMessageHandler(HttpWebResponseEventArgs response);
        delegate void AddHttpRequestMessageHandler(HttpRequestEventArgs request);

        delegate void AddGstMessageHandler(string type, string message);


        #endregion

        #region Window_Events

        void MainWindowClosed(object sender, EventArgs e)
        {
            _mainWindowIsClosed = true;
            _mediaHandler.StopLoop();
            _myDebugWindow.Close();
            _myIMWindow.Close();
            _mediaHandler.VideoWindow.Close();
            Save_Settings_to_Xml("Resources\\settings.xml", _settings);
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
            Application.Current.Shutdown();
        }

        void SettingsWindowClosed(object sender, EventArgs e)
        {
            Save_Settings_to_Xml("Resources\\settings.xml", _settings);
        }

        void MyAddressBookWindowClosed(object sender, EventArgs e)
        {
            if (_addressBook != null)
            {
                SaveAddressBook(_addressBook, _xdmsHandler);
            }
        }

        void MyDebugWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_mainWindowIsClosed) return;
            e.Cancel = true;
            Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
                                                       (DispatcherOperationCallback)(arg => { Show_Debug_Log_MenuItem.IsChecked = false; return null; }), null);
        }

        private void ShowDebugLogChecked(object sender, RoutedEventArgs e)
        {
            _myDebugWindow.Show();
        }

        private void ShowDebugLogUnchecked(object sender, RoutedEventArgs e)
        {
            _myDebugWindow.Hide();
        }

        void MyIMWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_mainWindowIsClosed)
            {
                IM_window imWindow = sender as IM_window;
                e.Cancel = true;
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background,
             (DispatcherOperationCallback)(arg =>
                                               {
                                                   if (imWindow != null) imWindow.Hide();
                                                   return null;
                                               }), null);
            }

        }


        #endregion

        #region Registration

        private void RegKnownUserClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            _settings.ims_private_user_identity = mi.Tag.ToString();
            _settings.ims_public_user_identity = "sip:" + mi.Tag;
            _settings.ims_password = mi.Tag.ToString().Remove(mi.Tag.ToString().IndexOf('@'));
            Register();
        }

        private void Register()
        {
            _app.Register(_settings.ims_private_user_identity.Split('@')[0], _settings.ims_password, _settings.ims_realm);
        }

        private void RegisterClick(object sender, RoutedEventArgs e)
        {
            Register();
        }

        private void DeregisterClick(object sender, RoutedEventArgs e)
        {
            if (_app.RegState.ToLower().Contains("registered")|| _app.RegState.ToLower().Contains("Registering"))
            {
                _app.Deregister(_settings.ims_public_user_identity);
                if (_settings.presence_enabled)
                {
                    _presenceHandler.Publish(_settings.ims_public_user_identity, "closed", "Offline", 3600);
                }
            }
        }

        #endregion

        #region Presence

        private void RetrieveStatusOfContacts()
        {
          Status_ListBox.Dispatcher.Invoke(
          DispatcherPriority.Normal,
          new Action(
              () => Status_ListBox.Items.Clear()));
            foreach (Contact contact in _addressBook.Entries)
            {
                AddStatusItemHandler handler = AddContactStatusItem;
                Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, contact);
                // TODO: Automatically subscribe to contact status;
            }
            foreach (Contact contact in _addressBook.Entries)
            {
                SubscribeToStatusHandler handler = SubscribeToStatus;
                Dispatcher.BeginInvoke(DispatcherPriority.Background, handler, contact);
                // TODO: Automatically subscribe to contact status;
            }
        }

        private void SubscribeToStatus(Contact contact)
        {
            _presenceHandler.Subscribe(contact.SipUri);
        }

        delegate void AddStatusItemHandler(Contact contact);
        delegate void SubscribeToStatusHandler(Contact contact);

        private MenuItem Create_Menu_Item(string tag, string title)
        {
            MenuItem menuItem = new MenuItem {Tag = tag};
            TextBlock txtBlock = new TextBlock {Text = title};
            menuItem.Header = txtBlock;
            return menuItem;
        }

        private ContextMenu CreateContactContextMenu(string tag)
        {
            ContextMenu contextMenu = new ContextMenu();
            MenuItem tempMenuItem = Create_Menu_Item(tag, "Voice Call");
            tempMenuItem.Click += VoiceCallMenuItemClick;
            contextMenu.Items.Add(tempMenuItem);

            tempMenuItem = Create_Menu_Item(tag, "Video Call");
            tempMenuItem.Click += VideoCallMenuItemClick;
            contextMenu.Items.Add(tempMenuItem);

            tempMenuItem = Create_Menu_Item(tag, "Send Message");
            tempMenuItem.Click += SendMessageMenuItemClick;
            contextMenu.Items.Add(tempMenuItem);

            tempMenuItem = Create_Menu_Item(tag, "Subscribe to Presence");
            tempMenuItem.Click += SubscribePresenceMenuItemClick;
            contextMenu.Items.Add(tempMenuItem);

            return contextMenu;

        }

        private void SubscribePresenceMenuItemClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            _presenceHandler.Subscribe(mi.Tag.ToString());
        }

        void VoiceCallMenuItemClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            _callHandler.StartCall(mi.Tag.ToString(), false, _settings.audiocall_local_port, _settings.videocall_local_port);
        }

        void VideoCallMenuItemClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            _callHandler.StartCall(mi.Tag.ToString(), true, _settings.audiocall_local_port, _settings.videocall_local_port);
        }

        private void AddContactStatusItem(Contact contact)
        {
            try
            {
                StackPanel stackPanel = new StackPanel
                                            {
                                                Orientation = Orientation.Horizontal,
                                                HorizontalAlignment = HorizontalAlignment.Stretch,
                                                Background = Brushes.Transparent,
                                                ContextMenu = CreateContactContextMenu(contact.SipUri)
                                            };

                Image basic = new Image {Margin = new Thickness(10)};
                Binding myBinding = new Binding("Basic")
                                        {
                                            BindsDirectlyToSource = true,
                                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                                            Source = contact.GetStatus(),
                                            Converter = new StatusConverter()
                                        };
                basic.SetBinding(Image.SourceProperty, myBinding);
                basic.Width = 30;

                myBinding = new Binding("Basic")
                                {
                                    BindsDirectlyToSource = true,
                                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                                    Source = contact.GetStatus()
                                };
                basic.SetBinding(ToolTipProperty, myBinding);


                TextBlock note = new TextBlock
                                     {
                                         HorizontalAlignment = HorizontalAlignment.Center,
                                         VerticalAlignment = VerticalAlignment.Center
                                     };
                myBinding = new Binding("Note")
                                {
                                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                                    Source = contact.GetStatus()
                                };
                note.SetBinding(ToolTipProperty, myBinding);

                myBinding = new Binding("DisplayName")
                                {
                                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                                    Source = contact.GetStatus()
                                };
                note.SetBinding(TextBlock.TextProperty, myBinding);



                stackPanel.Children.Add(basic);
                stackPanel.Children.Add(note);
                ListBoxItem lbi = new ListBoxItem {Content = stackPanel};

                Status_ListBox.Items.Add(lbi);
                Status_ListBox.Items.Refresh();
            }
            catch (Exception e)
            {
                MessageBox.Show("Creating Status Item " + e.Message);
            }
        }

        void PresenceHandlerPresenceChangedEvent(object sender, PresenceHandler.PresenceChangedArgs e)
        {

            bool foundContact = false;
            int index = -1;
            int counter = 0;
            try
            {
                foreach (Contact contact in _addressBook.Entries)
                {
                    if (contact.SipUri == e.Contact)
                    {
                        Status status = contact.GetStatus();
                        status.Basic = e.Basis;
                        status.Note = e.Note;

                        foundContact = true;
                        index = counter;

                    }
                    counter++;
                }
                if (!foundContact)
                {
                    MessageBox.Show("Did not find contact for status update (" + e.Contact + ")");
                }
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error in Presence Status Update : " + exc.Message);
            }
        }
        #endregion

        #region IM

        delegate void AddMsgToConvHandler(string contact, string message);
        private void AddMsgToConversation(string contact, string message)
        {
            if (!MessageTabExists(Helpers.RemoveAngelBrackets(contact)))
            {
                CreateMessageTab(contact);
            }

            foreach (TabItem tabItem in _myIMWindow.IM_TabControl.Items)
            {
                if (tabItem.Tag.ToString() != Helpers.RemoveAngelBrackets(contact)) continue;
                DockPanel dockPanel = tabItem.Content as DockPanel;

                if (dockPanel != null)
                {
                    Label statusLabel = dockPanel.Children[1] as Label;
                    if (statusLabel != null) statusLabel.Content = "Message Recieved";
                }
                if (dockPanel == null) continue;
                RichTextBox textBox = dockPanel.Children[2] as RichTextBox;
                if (textBox == null) continue;
                FlowDocument flowDoc = textBox.Document;

                Paragraph para = new Paragraph();
                Span username = new Span {Foreground = Brushes.Red};
                string name = Helpers.RemoveAngelBrackets(contact);
                name = name.Substring(4, name.IndexOf('@') - 4);
                name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                username.Inlines.Add(name + ": ");

                para.Inlines.Add(username);

                para.Inlines.Add(message);
                flowDoc.Blocks.Add(para);

                //text_box.Foreground = Brushes.Red;
                //text_box.AppendText(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(contact.Substring(4, contact.IndexOf('@') - 4)) + ":");
                //text_box.Foreground = Brushes.Black;
                //text_box.AppendText(message+"\n");
            }
            _myIMWindow.Show();
        }

        void IMHandlerMessageRecievedEvent(object sender, IMHandler.MessageReceivedArgs e)
        {
            AddMsgToConvHandler handler = AddMsgToConversation;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, e.Contact, e.Message);
        }

        delegate void UpdateIMMessageStatusHandler(string contact, string status);
        
        private void UpdateIMMessageStatus(string contact, string status)
        {
            foreach (TabItem tabItem in _myIMWindow.IM_TabControl.Items)
            {
                if (tabItem.Tag.ToString() == contact)
                {
                    DockPanel dockPanel = tabItem.Content as DockPanel;

                    if (dockPanel != null)
                    {
                        Label statusLabel = dockPanel.Children[1] as Label;
                        if (statusLabel != null)
                        {
                            string name = Helpers.RemoveAngelBrackets(contact);
                            name = name.Substring(4, name.IndexOf('@') - 4);
                            name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                            statusLabel.Content = name + " is typing";
                        }
                    }
                }
            }
        }

        void IMMessageStatusEvent(object sender, IMHandler.TypingMessageRecievedArgs e)
        {
            UpdateIMMessageStatusHandler handler = UpdateIMMessageStatus;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, handler, e.Contact, e.Message);
        }

        bool MessageTabExists(string uri)
        {
            return _myIMWindow.IM_TabControl.Items.Cast<TabItem>().Any(tabItem => tabItem.Tag.ToString() == uri);
        }

        void CreateMessageTab(string uri)
        {
            TabItem tabItem = new TabItem();
            DockPanel overallDockPanel = new DockPanel();
            DockPanel sendDockPanel = new DockPanel();
            TextBox textBox = new TextBox
                                  {
                                      VerticalAlignment = VerticalAlignment.Stretch,
                                      VerticalContentAlignment = VerticalAlignment.Center
                                  };
            textBox.TextChanged += SendIMTextChanged;
            textBox.Tag = uri;

            RichTextBox conversationBox = new RichTextBox();

            FlowDocument conversationFlowDoc = new FlowDocument();
            conversationBox.Document = conversationFlowDoc;

            ImageButton imageButton = new ImageButton
                                          {
                                              ImageOver = "Status_Images/available.png",
                                              ImageDown = "Status_Images/Offline.png",
                                              ImageNormal = "Status_Images/Unknown.png",
                                              Text = "Send",
                                              Style = (Style) FindResource("Image_Button_With_text"),
                                              Width = 60,
                                              Height = 30
                                          };
            imageButton.Click += SendIMButtonClicked;
            imageButton.Tag = Helpers.RemoveAngelBrackets(uri);

            conversationBox.VerticalAlignment = VerticalAlignment.Stretch;
            conversationBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            conversationBox.Background = Brushes.White;
            conversationBox.IsReadOnly = true;


            Border border = new Border {Style = (Style) FindResource("MainBorder"), Child = imageButton};

            sendDockPanel.Children.Add(border);
            sendDockPanel.Children.Add(textBox);


            Label statusLabel = new Label {Content = ""};


            DockPanel.SetDock(sendDockPanel, Dock.Bottom);
            DockPanel.SetDock(imageButton, Dock.Right);
            DockPanel.SetDock(textBox, Dock.Left);
            DockPanel.SetDock(statusLabel, Dock.Bottom);
            DockPanel.SetDock(conversationBox, Dock.Top);




            overallDockPanel.Children.Add(sendDockPanel);
            overallDockPanel.Children.Add(statusLabel);
            overallDockPanel.Children.Add(conversationBox);

            tabItem.Content = overallDockPanel;
            string name = Helpers.RemoveAngelBrackets(uri);
            name = name.Substring(4, name.IndexOf('@') - 4);
            name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
            tabItem.Header = name;

            tabItem.Tag = Helpers.RemoveAngelBrackets(uri);

            _myIMWindow.IM_TabControl.Items.Add(tabItem);
        }

        void SendIMTextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox != null && textBox.Text != "" && textBox.Text.Length > 5)
            {
                _imHandler.SendTypingNotice(textBox.Tag.ToString());
            }
        }

        void SendMessageMenuItemClick(object sender, RoutedEventArgs e)
        {
            MenuItem mi = sender as MenuItem;
            if (!MessageTabExists(mi.Tag.ToString()))
            {
                CreateMessageTab(mi.Tag.ToString());
            }
            _myIMWindow.Show();
        }

        void SendIMButtonClicked(object sender, RoutedEventArgs e)
        {
            ImageButton imgButton = sender as ImageButton;
            TabItem conversationTab = null;
            foreach (TabItem tabItem in _myIMWindow.IM_TabControl.Items)
            {
                if (imgButton != null && tabItem.Tag.ToString() == imgButton.Tag.ToString())
                {
                    conversationTab = tabItem;
                }
            }

            if (conversationTab != null)
            {
                DockPanel dockPanel = conversationTab.Content as DockPanel;
                if (dockPanel != null)
                {
                    DockPanel sendPanel = dockPanel.Children[0] as DockPanel;
                    if (sendPanel != null)
                    {
                        TextBox textBox = sendPanel.Children[1] as TextBox;

                        if (textBox != null)
                        {
                            string message = textBox.Text;
                            textBox.Text = "";

                            _imHandler.SendMessage(imgButton.Tag.ToString(), message);

                            RichTextBox richTextBox = dockPanel.Children[2] as RichTextBox;
                            if (richTextBox != null)
                            {
                                FlowDocument flowDoc = richTextBox.Document;

                                Paragraph para = new Paragraph();
                                Span username = new Span {Foreground = Brushes.Green};
                                string name = Helpers.RemoveAngelBrackets(_settings.ims_public_user_identity);
                                name = name.Substring(4, name.IndexOf('@') - 4);
                                name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
                                username.Inlines.Add(name + ": ");
            
                                para.Inlines.Add(username);

                                para.Inlines.Add(message);
                                flowDoc.Blocks.Add(para);
                            }
                        }
                    }
                }
            }
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

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            SettingsWindow mySettingsWindow = new SettingsWindow {SizeToContent = SizeToContent.WidthAndHeight};

            TabControl optionsTabControl = mySettingsWindow.Options_tab_control;

            PropertyInfo[] properties = typeof(Preferences).GetProperties();


            if (_settings != null)
            {
                Dictionary<string, int> dictCounter = new Dictionary<string, int>();

                foreach (string section in _settings.option_sections)
                {
                    dictCounter.Add(section.ToLower(), 0);
                    TabItem sectionTabItem = new TabItem {Name = section.ToLower(), Header = section};

                    DockPanel dock = new DockPanel();
                    Grid optionsGrid = new Grid
                                           {
                                               HorizontalAlignment = HorizontalAlignment.Stretch,
                                               VerticalAlignment = VerticalAlignment.Stretch
                                           };


                    ColumnDefinition coldef = new ColumnDefinition();
                    //coldef.Width = GridLength.;
                    optionsGrid.ColumnDefinitions.Add(coldef);
                    coldef = new ColumnDefinition();
                    //coldef.Width = GridLength.Auto;
                    optionsGrid.ColumnDefinitions.Add(coldef);
                    dock.Children.Add(optionsGrid);

                    sectionTabItem.Content = dock;
                    optionsTabControl.Items.Add(sectionTabItem);
                }

                foreach (PropertyInfo propInfo in properties)
                {
                    RowDefinition rowdef = new RowDefinition();
                    //rowdef.Height = GridLength.Auto;
                    string section = propInfo.Name.Remove(propInfo.Name.IndexOf('_'));
                    Grid optionsGrid = null;
                    foreach (TabItem tabItem in optionsTabControl.Items)
                    {
                        if (tabItem.Name.ToLower() == section)
                        {
                            DockPanel dock = tabItem.Content as DockPanel;
                            if (dock != null) optionsGrid = dock.Children[0] as Grid;
                        }
                    }


                    if (optionsGrid != null) optionsGrid.RowDefinitions.Add(rowdef);

                    UIElement newControl = AddSettingsItem(propInfo.Name);
                    if (newControl != null)
                    {
                        TextBlock name = new TextBlock();
                        string optionName = propInfo.Name.Remove(0, section.Length + 1);
                        optionName = optionName.Replace("_", " ");
                        CultureInfo cultureInfo = Thread.CurrentThread.CurrentCulture;
                        TextInfo textInfo = cultureInfo.TextInfo;
                        name.Text = textInfo.ToTitleCase(optionName);
                        name.Margin = new Thickness(2);
                        //name.HorizontalContentAlignment = HorizontalAlignment.Center;
                        name.HorizontalAlignment = HorizontalAlignment.Center;
                        //name.VerticalContentAlignment = VerticalAlignment.Center;
                        name.VerticalAlignment = VerticalAlignment.Center;

                        if (optionsGrid != null)
                        {
                            optionsGrid.Children.Add(newControl);
                            optionsGrid.Children.Add(name);
                        }

                        Grid.SetColumn(newControl, 1);
                        Grid.SetRow(newControl, dictCounter[section]);

                        Grid.SetColumn(name, 0);
                        Grid.SetRow(name, dictCounter[section]);
                        dictCounter[section] = dictCounter[section] + 1;
                        if (newControl is ComboBox)
                        {
                            Load_Defaults_ComboBox((ComboBox)newControl, optionName);
                        }
                    }
                }
            }
            mySettingsWindow.Show();
            mySettingsWindow.SizeToContent = SizeToContent.Manual;
            mySettingsWindow.Closed += SettingsWindowClosed;
        }

        private void Load_Defaults_ComboBox(ComboBox newControl,string optionName)
        {
            if (optionName.ToLower().Contains("auth"))
            {
                newControl.Items.Add("MD5");
                newControl.Items.Add("AKAv1-MD5");
            }
        }

        private UIElement AddSettingsItem(string propertyName)
        {
            Binding myBinding = new Binding(propertyName) {Source = _settings};

            if (_settings.setting_item_types.ContainsKey(propertyName))
            {
                switch (_settings.setting_item_types[propertyName])
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
            TextBox genericTextBox = new TextBox();
            genericTextBox.SetBinding(TextBox.TextProperty, myBinding);
            genericTextBox.MaxHeight = 30;
            genericTextBox.MaxWidth = 200;
            genericTextBox.HorizontalContentAlignment = HorizontalAlignment.Center;
            genericTextBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            genericTextBox.VerticalContentAlignment = VerticalAlignment.Center;
            genericTextBox.VerticalAlignment = VerticalAlignment.Stretch;
            return genericTextBox;
        }

        #endregion

        #region General_Stack
        void MyUserAgentIncomingCall(object sender, Message message)
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

        private void AnswerCallClick(object sender, RoutedEventArgs e)
        {
            Action workAction = delegate
            {
                System.ComponentModel.BackgroundWorker worker = new System.ComponentModel.BackgroundWorker();
                worker.DoWork += delegate
                {
                    _callHandler.ReceiveCall();
                };
                worker.RunWorkerAsync();
            };
            Dispatcher.BeginInvoke(DispatcherPriority.Background, workAction);
            _soundPlayer.Dispatcher.Invoke(
                 DispatcherPriority.Normal,
                 new Action(
                     () => _soundPlayer.Stop()));
            _callHandler.SetState(CallState.Active);
        }

        private void CancelCallClick(object sender, RoutedEventArgs e)
        {
            //TODO Check Cancel Call
            _soundPlayer.Dispatcher.Invoke(
                 DispatcherPriority.Normal,
                 new Action(
                     () => _soundPlayer.Stop()));
            _callHandler.CancelCall(null);
        }

        private void ImageImageFailed(object sender, ExceptionRoutedEventArgs e)
        {

        }

    }
}
