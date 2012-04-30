using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using log4net;
using SIPLib;

namespace IMS_client
{
    public class SIPApp : SIPLib.SIPApp
    {
        public override SIPStack stack { get; set; }
        public string username { get; set; }
        public string realm { get; set; }
        public string password { get; set; }

        private byte[] temp_buffer { get; set; }

        public override TransportInfo transport { get; set; }

        public  UserAgent registerUA { get; set; }

        private UserAgent callUA { get; set; }

        public UserAgent messageUA { get; set; }

        public event EventHandler<RawEventArgs> Raw_Recv_Event;
        public event EventHandler<RawEventArgs> Raw_Sent_Event;
        public event EventHandler<SipMessageEventArgs> Request_Recv_Event;
        public event EventHandler<SipMessageEventArgs> Response_Recv_Event;
        public event EventHandler<SipMessageEventArgs> Sip_Sent_Event;
        public event EventHandler<StackErrorEventArgs> Error_Event;
        public event EventHandler<RegistrationChangedEventArgs> Reg_Event;

        private static ILog _log = LogManager.GetLogger(typeof(SIPApp));

        public SIPApp(TransportInfo transport)
        {
            log4net.Config.XmlConfigurator.Configure();
            this.temp_buffer = new byte[4096];
            if (transport.type == ProtocolType.Tcp)
            {
                transport.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            else
            {
                transport.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            }
            IPEndPoint localEP = new IPEndPoint(transport.host, transport.port);
            transport.socket.Bind(localEP);

            IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
            EndPoint sendEP = (EndPoint)sender;
            transport.socket.BeginReceiveFrom(temp_buffer, 0, temp_buffer.Length, SocketFlags.None, ref sendEP, new AsyncCallback(ReceiveDataCB), sendEP);
            this.transport = transport;
        }

        public void Register(string uri)
        {
            if (this.Reg_Event != null)
            {
                this.Reg_Event(this, new RegistrationChangedEventArgs("Registering", null));
            }
            this.registerUA = new UserAgent(this.stack, null, false);
            Message register_msg = this.registerUA.createRegister(new SIPURI(uri));
            register_msg.insertHeader(new Header("3600", "Expires"));
            this.registerUA.sendRequest(register_msg);
        }

        public void ReceiveDataCB(IAsyncResult asyncResult)
        {
            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                EndPoint sendEP = (EndPoint)sender;
                int bytesRead = transport.socket.EndReceiveFrom(asyncResult, ref sendEP);
                string data = ASCIIEncoding.ASCII.GetString(temp_buffer, 0, bytesRead);
                string remote_host = ((IPEndPoint)sendEP).Address.ToString();
                string remote_port = ((IPEndPoint)sendEP).Port.ToString();
                if (this.Raw_Recv_Event != null)
                {
                    this.Raw_Recv_Event(this, new RawEventArgs(data, new string[] { remote_host, remote_port }));
                }
                this.transport.socket.BeginReceiveFrom(this.temp_buffer, 0, this.temp_buffer.Length, SocketFlags.None, ref sendEP, new AsyncCallback(this.ReceiveDataCB), sendEP);
            }
            catch (Exception ex)
            {
                if (this.Error_Event != null)
                {
                    this.Error_Event(this, new StackErrorEventArgs("Receive Data Callback", ex));
                }
            }
        }

        public override void send(string data, string ip, int port, SIPStack stack)
        {
            IPAddress[] addresses = System.Net.Dns.GetHostAddresses(ip);
            IPEndPoint dest = new IPEndPoint(addresses[0], port);
            EndPoint destEP = (EndPoint)dest;
            byte[] send_data = ASCIIEncoding.ASCII.GetBytes(data);
            string remote_host = ((IPEndPoint)destEP).Address.ToString();
            string remote_port = ((IPEndPoint)destEP).Port.ToString();

            stack.transport.socket.BeginSendTo(send_data, 0, send_data.Length, SocketFlags.None, destEP, new AsyncCallback(this.SendDataCB), destEP);
            if (this.Raw_Sent_Event != null)
            {
                this.Raw_Sent_Event(this, new RawEventArgs(data, new string[] { remote_host, remote_port }));
            }
            if (this.Sip_Sent_Event != null)
            {
                this.Sip_Sent_Event(this, new SipMessageEventArgs(new Message(data)));
            }
        }

        private void SendDataCB(IAsyncResult asyncResult)
        {
            try
            {
                stack.transport.socket.EndSend(asyncResult);
            }
            catch (Exception ex)
            {
                _log.Error("Error in sendDataCB", ex);
                if (this.Error_Event != null)
                {
                    this.Error_Event(this, new StackErrorEventArgs("Send Data Callback", ex));
                }
            }
        }

        public override UserAgent createServer(Message request, SIPURI uri, SIPStack stack)
        {
            if (request.method == "INVITE")
            {
                return new UserAgent(this.stack, request);
            }
            else return null;
        }

        public override void sending(UserAgent ua, Message message, SIPStack stack)
        {
            if (Utils.isRequest(message))
            {
                _log.Info("Sending request with method " + message.method);
            }
            else
            {
                _log.Info("Sending response with code " + message.response_code);
            }
            _log.Debug("\n\n" + message.ToString());
            //TODO: Allow App to modify message before it gets sent?;
        }

        public override void cancelled(UserAgent ua, Message request, SIPStack stack)
        {
            throw new NotImplementedException();
        }

        public override void dialogCreated(Dialog dialog, UserAgent ua, SIPStack stack)
        {
            this.callUA = dialog;
            _log.Info("New dialog created");
        }

        public override Timer createTimer(UserAgent app, SIPStack stack)
        {
            return new Timer(app);
        }

        public override string[] authenticate(UserAgent ua, Header header, SIPStack stack)
        {
            return new string[] { this.username + "@" + this.realm, this.password };
        }

        public override void receivedResponse(UserAgent ua, Message response, SIPStack stack)
        {
            _log.Info("Received response with code " + response.response_code + " " + response.response_text);
            _log.Debug("\n\n" + response.ToString());
            if (this.Response_Recv_Event != null)
            {
                this.Response_Recv_Event(this, new SipMessageEventArgs(response));
            }
        }

        public override void receivedRequest(UserAgent ua, Message request, SIPStack stack)
        {
            _log.Info("Received request with method " + request.method.ToUpper());
            _log.Debug("\n\n" + request.ToString());
            if (this.Request_Recv_Event != null)
            {
                this.Request_Recv_Event(this, new SipMessageEventArgs(request,ua));
            }

        }

        public void timeout(Transaction transaction)
        {
            throw new NotImplementedException();
        }

        public void error(Transaction transaction, string error)
        {
            throw new NotImplementedException();
        }

        public void SendIM(string uri, string message,string content_type = "text/plain")
        {
            uri = checkURI(uri);
            if (isRegistered())
            {
                this.messageUA = new UserAgent(this.stack);
                this.messageUA.localParty = this.registerUA.localParty;
                this.messageUA.remoteParty = new Address(uri);
                Message m = this.messageUA.createRequest("MESSAGE", message);
                m.insertHeader(new Header("", "Content-Type"));
                this.messageUA.sendRequest(m);
            }
        }

        public void endCurrentCall()
        {
            if (isRegistered())
            {
                if (this.callUA != null)
                {
                    try
                    {
                        Dialog d = (Dialog)this.callUA;
                        Message bye = d.createRequest("BYE");
                        d.sendRequest(bye);
                    }
                    catch (InvalidCastException E)
                    {
                        _log.Error("Error ending current call, Dialog Does not Exist ?", E);
                    }

                }
                else
                {
                    _log.Error("Call UA does not exist, not sending CANCEL message");
                }

            }
            else
            {
                _log.Error("Not registered, not sending CANCEL message");
            }

        }

        public void Invite(string uri)
        {
            uri = checkURI(uri);
            if (isRegistered())
            {
                this.callUA = new UserAgent(this.stack, null, false);
                this.callUA.localParty = this.registerUA.localParty;
                this.callUA.remoteParty = new Address(uri);
                Message invite = this.callUA.createRequest("INVITE");
                this.callUA.sendRequest(invite);
            }
            else
            {
                _log.Error("isRegistered failed in invite message");
            }
        }

        private string checkURI(string uri)
        {
            if (!uri.Contains("<sip:") && !uri.Contains("sip:"))
            {
                uri = "<sip:" + uri + ">";
            }
            return uri;
        }

        private bool isRegistered()
        {
            if (this.registerUA == null || this.registerUA.localParty == null)
                return false;
            else return true;
        }

        public void Register(string username, string password, string realm)
        {
            this.username = username;
            this.password = password;
            this.realm = realm;
            this.registerUA = new UserAgent(this.stack, null, false);
            Message register_msg = this.registerUA.createRegister(new SIPURI("sip:"+username+"@"+realm));
            register_msg.insertHeader(new Header("3600", "Expires"));
            this.registerUA.sendRequest(register_msg);
        }

        public void Register(string uri)
        {
            this.registerUA = new UserAgent(this.stack, null, false);
            Message register_msg = this.registerUA.createRegister(new SIPURI(uri));
            register_msg.insertHeader(new Header("3600", "Expires"));
            this.registerUA.sendRequest(register_msg);
        }

        public void Invite(string uri)
        {
            uri = checkURI(uri);
            if (isRegistered())
            {
                this.callUA = new UserAgent(this.stack, null, false);
                this.callUA.localParty = this.registerUA.localParty;
                this.callUA.remoteParty = new Address(uri);
                Message invite = this.callUA.createRequest("INVITE");
                this.callUA.sendRequest(invite);
            }
            else
            {
                _log.Error("isRegistered failed in invite message");
            }
        }

        public  void Invite(string uri, SDP sdp)
        {
            uri = checkURI(uri);
            if (isRegistered())
            {
                this.callUA = new UserAgent(this.stack, null, false);
                this.callUA.localParty = this.registerUA.localParty;
                this.callUA.remoteParty = new Address(uri);
                Message invite = this.callUA.createRequest("INVITE");
                invite.insertHeader(new Header("application/sdp", "Content-Type"));
                invite.body = sdp.ToString();
                this.callUA.sendRequest(invite);
            }
            else
            {
                _log.Error("isRegistered failed in invite message");
            }
        }

        internal void acceptCall(SDP sdp)
        {
            Message response = this.callUA.createResponse(200, "OK");
            response.insertHeader(new Header("application/sdp", "Content-Type"));
            response.body = sdp.ToString();
            this.callUA.sendResponse(response);
        }

        internal void stopCall(SDP sdp)
        {
            Message response = this.callUA.createResponse(200, "OK");
            response.insertHeader(new Header("application/sdp", "Content-Type"));
            response.body = sdp.ToString();
            this.callUA.sendResponse(response);
        }
    }
}
