using System;
using System.Text;
using SIPLib.SIP;
using SIPLib.utils;

namespace IMS_client
{

    class CallHandler
    {
        readonly Preferences _settings;
        readonly SIPApp _app;
        public CallState CallState;
        readonly MultimediaHandler _mediaHandler;
        readonly int _localAudioPort;
        readonly int _localVideoPort;
        int _remoteAudioPort;
        int _remoteVideoPort;
        public bool InCall;

        public UserAgent UA { get; set; }

        public Message IncomingCall;
        private Message _outgoingInvite;

        public event EventHandler StateChanged = null;

        private void OnStateChanged(CallState state)
        {
            if (StateChanged != null)
            {
                StateChanged(this, new EventArgs());
            }
        }

        public CallHandler(SIPApp app, Preferences settings, MultimediaHandler mediaHandler, int localAudioPort, int localVideoPort)
        {
            _app = app;
            _settings = settings;
            _mediaHandler = mediaHandler;
            _localAudioPort = localAudioPort;
            _localVideoPort = localVideoPort;
        }

        public void StartCall(string toUri, bool videoEnabled, int localAudioPort, int localVideoPort)
        {
            InCall = true;
            SDP sdp = new SDP(GenerateSDP(videoEnabled, localAudioPort, localVideoPort));
            _app.Invite(toUri,sdp);
        }

        private string GenerateSDP(bool videoEnabled, int localAudioPort, int localVideoPort)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("v=0\n");
            sb.Append("o=- 0 0 IN IP4 " + _settings.ims_ip_address + "\n");
            sb.Append("s=IMS Call\n");
            sb.Append("c=IN IP4 " + _settings.ims_ip_address + "\n");
            sb.Append("t=0 0\n");
            sb.Append("m=audio " + localAudioPort + " RTP/AVP 3 0 101\n");
            sb.Append("b=AS:64\n");
            sb.Append("a=rtpmap:3 GSM/8000\n");
            sb.Append("a=rtpmap:0 PCMU/8000\n");
            sb.Append("a=rtpmap:101 telephone-event/8000\n");
            sb.Append("a=fmtp:101 0-11\n");

            if (videoEnabled)
            {
                sb.Append("m=video " + localVideoPort + " RTP/AVP 96 \n");
                sb.Append("b=AS:128 \n");
                sb.Append("a=rtpmap:96 H263-1998 \n");
                sb.Append("a=fmtp:96 profile-level-id=0 \n");
            }
            return sb.ToString();
        }

        public void ReceiveCall()
        {
            if (!(InCall) && (IncomingCall != null))
            {
                string remoteIP = "not_found";
                bool videoEnabled = false;
                if (IncomingCall.Headers.ContainsKey("Content-Type") && IncomingCall.First("Content-Type").ToString().ToLower().Contains("application/sdp"))
                {
                    SDP remoteSDP = new SDP(IncomingCall.Body);
                    remoteIP = remoteSDP.Connection.Address;
                    foreach (SDPMedia media in remoteSDP.Media)
                    {
                        if (media.ToString().ToLower().Contains("audio"))
                        {
                            _remoteAudioPort = Int32.Parse(media.Port);
                        }
                        else if (media.ToString().ToLower().Contains("video"))
                        {
                            _remoteVideoPort = Int32.Parse(media.Port);
                            videoEnabled = true;
                        }
                    }
                }

                SDP sdp = new SDP(GenerateSDP(videoEnabled, _localAudioPort, _localVideoPort));
                _app.AcceptCall(sdp);

                InCall = true;
                CallState = CallState.Active;

                _mediaHandler.StartAudioRx(_settings.audiocall_local_port, 8);
                _mediaHandler.StartAudioTx(remoteIP, _remoteAudioPort, 8);

                if (videoEnabled)
                {
                    _mediaHandler.StartVideoTx(remoteIP, _remoteVideoPort);
                    _mediaHandler.StartVideoRx(_settings.videocall_local_port, Utils.Unquote(IncomingCall.First("From").ToString()));
                }


            }
        }

        public void ProcessResponse(Message message)
        {
            string remoteIP = "not_found";

            if (message.StatusCodeType == StatusCodes.Informational)
            {
               
            }
            else if (message.StatusCodeType == StatusCodes.Successful)
            {

                if ((CallState == CallState.Ringing) || (CallState == CallState.Calling))
                {
                    SetState(CallState.Active);

                    //TODO This should not be needed as the stack should create ACKs
                    //Message request = stack.CreateAck(outgoing_invite);
                    //stack.SendMessage(request);

                    bool videoEnabled = false;
                    if (message.Headers.ContainsKey("Content-Type") && message.First("Content-Type").ToString().ToLower().Contains("application/sdp"))
                    {
                        SDP remoteSDP = new SDP(IncomingCall.Body);
                        remoteIP = remoteSDP.Connection.Address;
                        foreach (SDPMedia media in remoteSDP.Media)
                        {
                            if (media.ToString().ToLower().Contains("audio"))
                            {
                                _remoteAudioPort = Int32.Parse(media.Port);
                            }
                            else if (media.ToString().ToLower().Contains("video"))
                            {
                                videoEnabled = true;
                                _remoteVideoPort = Int32.Parse(media.Port);
                            }
                        }
                    }

                    _mediaHandler.StartAudioRx(_settings.audiocall_local_port, 8);
                    _mediaHandler.StartAudioTx(remoteIP, _remoteAudioPort, 8);
                    if (videoEnabled)
                    {
                        _mediaHandler.StartVideoRx(_settings.videocall_local_port, Utils.Unquote(_outgoingInvite.First("To").ToString()));
                        _mediaHandler.StartVideoTx(remoteIP, _remoteVideoPort);
                    }

                }
                else if (CallState == CallState.Ending)
                {
                    SetState(CallState.Ended);
                }

            }
            else if (message.StatusCodeType == StatusCodes.GlobalFailure)
            {
                if (message.ResponseCode == 603)
                {
                    SetState(CallState.Ending);
                    IncomingCall = null;
                    _outgoingInvite = null;
                    InCall = false;
                }
            }
        }

        void CancelResponseReceived(object sender, Message message)
        {
            if (message.StatusCodeType == StatusCodes.ClientFailure)
            {
                SetState(CallState.Ended);
                //TODO Should not be needed here - stack should auto create acks
                //Message request = stack.CreateAck(incoming_call);
                //stack.SendMessage(request);
                IncomingCall = null;
                _outgoingInvite = null;
                InCall = false;
            }
        }


        public void SetState(CallState state)
        {
            CallState = state;
            OnStateChanged(state);
        }

        internal void StopCall()
        {
            //TODO Check ending of call
            //string uri = "";
            //Message temp = null;
            //if (incoming_call !=null)
            //{
            //    temp = incoming_call;
            //    uri = Utils.unquote(outgoing_invite.first("From").ToString());
               
            //}
            //else if (outgoing_invite != null)
            //{
            //    temp = outgoing_invite;
            //    uri = outgoing_invite.uri.ToString();
            //}
            //Message request = new Message(temp.ToString());
            //request.method = "BYE";
            //request.uri = new SIPURI(uri);
            //this.ua.createRequest("BYE");
            //this.app.endCurrentCall
            SetState(CallState.Ended);
            _app.EndCurrentCall();
            IncomingCall = null;
            _outgoingInvite = null;
            InCall = false;
            _mediaHandler.StopAudioRx();
            _mediaHandler.StopAudioTx();
            _mediaHandler.StopVideoRx();
            _mediaHandler.StopVideoTx();
        }

        internal void CancelCall(Message e)
        {
            if (InCall)
            {
                if (CallState == CallState.Active)
                {
                    StopCall();
                }
                else
                {
                    //SIP_Request cancel = stack.CreateRequest(SIP_Methods.CANCEL, new SIP_t_NameAddress(outgoing_invite.m_pHeader.Get("To:")[0].Value), new SIP_t_NameAddress(settings.ims_public_user_identity));
                    //foreach (SIP_HeaderField Header in outgoing_invite.m_pHeader)
                    //{
                    //    if (!Header.Name.ToUpper().Contains("TO") && !Header.Name.ToUpper().Contains("FROM"))
                    //    {
                    //        cancel.m_pHeader.Set(Header.Name, Header.Value);
                    //    }
                    //}
                    //cancel.m_pHeader = outgoing_invite.m_pHeader;
                    //cancel.CSeq = new SIP_t_CSeq(cancel.CSeq.SequenceNumber, SIP_Methods.CANCEL);

                    //SIP_RequestSender cancel_sender = stack.CreateRequestSender(cancel);
                    //cancel_sender.ResponseReceived += new EventHandler<SIP_ResponseReceivedEventArgs>(cancel_ResponseReceived);
                    //cancel_sender.Start();
                }
            }
            else if (e != null)
            {
                if (e.Method.ToUpper().Equals("CANCEL"))
                {
                    SetState(CallState.Ended);
                    //SIP_Response response = stack.CreateResponse(SIP_ResponseCodes.x487_Request_Terminated, e.Request);
                    //e.ServerTransaction.SendResponse(response);
                }

            }
        }
    }
}
