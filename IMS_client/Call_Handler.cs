using System;
using System.Text;
using SIPLib.SIP;
using SIPLib.Utils;

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
        public String CurrentCallID;

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
            _app.Invite(toUri, sdp);
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
                _app.AcceptCall(sdp, IncomingCall);

                InCall = true;
                CallState = CallState.Active;
                CurrentCallID = IncomingCall.First("Call-ID").Value.ToString();

                //TODO RE-ENABLE MEDIA HANDLING
                //_mediaHandler.StartAudioRx(_settings.audiocall_local_port, 8);
                //_mediaHandler.StartAudioTx(remoteIP, _remoteAudioPort, 8);

                if (videoEnabled)
                {
                    //TODO RE-ENABLE MEDIA HANDLING
                    //_mediaHandler.StartVideoTx(remoteIP, _remoteVideoPort);
                    //_mediaHandler.StartVideoRx(_settings.videocall_local_port, Helpers.Unquote(IncomingCall.First("From").ToString()));
                }


            }
        }

        private void ProcessInviteResponse(Message response)
        {
            string remoteIP = "not_found";
            if ((CallState == CallState.Ringing) || (CallState == CallState.Calling))
            {
                CurrentCallID = response.First("Call-ID").Value.ToString();
                SetState(CallState.Active);
                bool videoEnabled = false;
                if (response.Headers.ContainsKey("Content-Type") && response.First("Content-Type").ToString().ToLower().Contains("application/sdp"))
                {
                    SDP remoteSDP = new SDP(response.Body);
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
                //TODO RE-ENABLE MEDIA HANDLING
                //_mediaHandler.StartAudioRx(_settings.audiocall_local_port, 8);
                //_mediaHandler.StartAudioTx(remoteIP, _remoteAudioPort, 8);
                if (videoEnabled)
                {
                    //TODO RE-ENABLE MEDIA HANDLING
                    //_mediaHandler.StartVideoRx(_settings.videocall_local_port, Helpers.Unquote(_outgoingInvite.First("To").ToString()));
                    //_mediaHandler.StartVideoTx(remoteIP, _remoteVideoPort);
                }
            }
        }

        private void ProcessByeResponse(Message response)
        {
            if (CallState == CallState.Ending)
            {
                SetState(CallState.Ended);
                IncomingCall = null;
                _outgoingInvite = null;
                InCall = false;
            }
        }

        public void ProcessResponse(Message response)
        {
            if (response.StatusCodeType == StatusCodes.Informational)
            {
                if (response.ResponseCode == 100)
                {
                    SetState(CallState.Calling);
                }
                else if (response.ResponseCode == 180)
                {
                    SetState(CallState.Ringing);
                }
                else if (response.ResponseCode == 182)
                {
                    SetState(CallState.Queued);
                }
            }
            else if (response.StatusCodeType == StatusCodes.Successful)
            {
                string requestType = response.First("CSeq").ToString().Trim().Split()[1].ToUpper();
                switch (requestType)
                {
                    case "INVITE":
                        ProcessInviteResponse(response);
                        break;
                    case "BYE":
                        ProcessByeResponse(response);
                        break;
                    default:
                        System.Console.WriteLine("Bad response handed to Call Handler");
                        break;
                }
            }
            else if (response.StatusCodeType == StatusCodes.GlobalFailure)
            {
                if (response.ResponseCode == 603)
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

        internal void StopCall(CallState callState)
        {
            SetState(callState);
            if (callState == CallState.Ending)
            {
                _app.EndCall(CurrentCallID);    
            }
            IncomingCall = null;
            _outgoingInvite = null;
            InCall = false;

            // TODO RE-ENABLE media handling
            //_mediaHandler.StopAudioRx();
            //_mediaHandler.StopAudioTx();
            //_mediaHandler.StopVideoRx();
            //_mediaHandler.StopVideoTx();

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
        }

        internal void CancelCall()
        {
            if (InCall)
            {
                if (CallState == CallState.Active)
                {
                    StopCall(CallState.Ending);
                }
            }
        }
    }
}
