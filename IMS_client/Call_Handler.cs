using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using SIPLib;
using SIPLib.src.SIP;
using SIPLib.src;

namespace IMS_client
{

    class Call_Handler
    {
        Preferences settings;
        SIPApp app;
        public CallState call_state;
        Multimedia_Handler media_handler;
        int local_audio_port;
        int local_video_port;
        int remote_audio_port;
        int remote_video_port;
        public bool in_call = false;

        public UserAgent ua { get; set; }

        public Message incoming_call = null;
        private Message outgoing_invite = null;

        public event EventHandler StateChanged = null;

        private void OnStateChanged(CallState state)
        {
            if (this.StateChanged != null)
            {
                this.StateChanged(this, new EventArgs());
            }
        }

        public Call_Handler(SIPApp app, Preferences Settings, Multimedia_Handler Media_handler)
        {
            this.app = app;
            settings = Settings;
            media_handler = Media_handler;
        }

        public void Start_Call(string to_uri, bool video_enabled, int local_audio_port, int local_video_port)
        {
            in_call = true;
            SDP sdp = new SDP(Generate_SDP(video_enabled, local_audio_port, local_video_port));
            this.app.Invite(to_uri,sdp);
        }

        private string Generate_SDP(bool video_enabled, int local_audio_port, int local_video_port)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("v=0\n");
            sb.Append("o=- 0 0 IN IP4 " + settings.ims_ip_address + "\n");
            sb.Append("s=IMS Call\n");
            sb.Append("c=IN IP4 " + settings.ims_ip_address + "\n");
            sb.Append("t=0 0\n");
            sb.Append("m=audio " + local_audio_port + " RTP/AVP 3 0 101\n");
            sb.Append("b=AS:64\n");
            sb.Append("a=rtpmap:3 GSM/8000\n");
            sb.Append("a=rtpmap:0 PCMU/8000\n");
            sb.Append("a=rtpmap:101 telephone-event/8000\n");
            sb.Append("a=fmtp:101 0-11\n");

            if (video_enabled)
            {
                sb.Append("m=video " + local_video_port + " RTP/AVP 96 \n");
                sb.Append("b=AS:128 \n");
                sb.Append("a=rtpmap:96 H263-1998 \n");
                sb.Append("a=fmtp:96 profile-level-id=0 \n");
            }
            return sb.ToString();
        }

        public void Receive_Call()
        {
            if (!(in_call) && (incoming_call != null))
            {
                string remote_ip = "not_found";
                bool video_enabled = false;
                if (incoming_call.headers.ContainsKey("Content-Type") && incoming_call.first("Content-Type").ToString().ToLower().Contains("application/sdp"))
                {
                    SDP remote_sdp = new SDP(incoming_call.body);
                    remote_ip = remote_sdp.Connection.address;
                    foreach (SDPMedia media in remote_sdp.Media)
                    {
                        if (media.ToString().ToLower().Contains("audio"))
                        {
                            remote_audio_port = Int32.Parse(media.port);
                        }
                        else if (media.ToString().ToLower().Contains("video"))
                        {
                            remote_video_port = Int32.Parse(media.port);
                        }
                    }
                }

                SDP sdp = new SDP(Generate_SDP(video_enabled, local_audio_port, local_video_port));
                this.app.acceptCall(sdp);

                in_call = true;
                call_state = CallState.Active;

                media_handler.Start_Audio_Rx(settings.audiocall_local_port, 8);
                media_handler.Start_Audio_Tx(remote_ip, remote_audio_port, 8);

                if (video_enabled)
                {
                    media_handler.Start_Video_Tx(remote_ip, remote_video_port);
                    media_handler.Start_Video_Rx(settings.videocall_local_port, Utils.unquote(incoming_call.first("From").ToString()));
                }


            }
        }

        public void process_Response(Message message)
        {
            string remote_ip = "not_found";

            if (message.status_code_type == StatusCodes.Informational)
            {
               
            }
            else if (message.status_code_type == StatusCodes.Successful)
            {

                if ((this.call_state == CallState.Ringing) || (this.call_state == CallState.Calling))
                {
                    SetState(CallState.Active);

                    //TODO This should not be needed as the stack should create ACKs
                    //Message request = stack.CreateAck(outgoing_invite);
                    //stack.SendMessage(request);

                    bool video_enabled = false;
                    if (message.headers.ContainsKey("Content-Type") && message.first("Content-Type").ToString().ToLower().Contains("application/sdp"))
                    {
                        SDP remote_sdp = new SDP(incoming_call.body);
                        remote_ip = remote_sdp.Connection.address;
                        foreach (SDPMedia media in remote_sdp.Media)
                        {
                            if (media.ToString().ToLower().Contains("audio"))
                            {
                                remote_audio_port = Int32.Parse(media.port);
                            }
                            else if (media.ToString().ToLower().Contains("video"))
                            {
                                remote_video_port = Int32.Parse(media.port);
                            }
                        }
                    }

                    media_handler.Start_Audio_Rx(settings.audiocall_local_port, 8);
                    media_handler.Start_Audio_Tx(remote_ip, remote_audio_port, 8);
                    if (video_enabled)
                    {
                        media_handler.Start_Video_Rx(settings.videocall_local_port, Utils.unquote(outgoing_invite.first("To").ToString()));
                        media_handler.Start_Video_Tx(remote_ip, remote_video_port);
                    }

                }
                else if (this.call_state == CallState.Ending)
                {
                    SetState(CallState.Ended);
                }

            }
            else if (message.status_code_type == StatusCodes.GlobalFailure)
            {
                if (message.response_code == 603)
                {
                    SetState(CallState.Ending);
                    incoming_call = null;
                    outgoing_invite = null;
                    in_call = false;
                }
            }
        }

        void cancel_ResponseReceived(object sender, Message message)
        {
            if (message.status_code_type == StatusCodes.ClientFailure)
            {
                SetState(CallState.Ended);
                //TODO Should not be needed here - stack should auto create acks
                //Message request = stack.CreateAck(incoming_call);
                //stack.SendMessage(request);
                incoming_call = null;
                outgoing_invite = null;
                in_call = false;
            }
        }


        public void SetState(CallState state)
        {
            call_state = state;
            OnStateChanged(state);
        }

        internal void Stop_Call()
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
            this.app.endCurrentCall();
            incoming_call = null;
            outgoing_invite = null;
            in_call = false;
            media_handler.Stop_Audio_Rx();
            media_handler.Stop_Audio_Tx();
            media_handler.Stop_Video_Rx();
            media_handler.Stop_Video_Tx();
        }

        internal void Cancel_call(Message e)
        {
            if (in_call)
            {
                if (this.call_state == CallState.Active)
                {
                    Stop_Call();
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
                if (e.method.ToUpper().Equals("CANCEL"))
                {
                    SetState(CallState.Ended);
                    //SIP_Response response = stack.CreateResponse(SIP_ResponseCodes.x487_Request_Terminated, e.Request);
                    //e.ServerTransaction.SendResponse(response);
                }

            }
        }
    }
}
