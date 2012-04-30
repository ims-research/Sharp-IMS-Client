using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using SipStack;

namespace IMS_client
{

    class Call_Handler
    {

        ClientSipStack stack;
        Preferences settings;
        public CallState call_state;
        Multimedia_Handler media_handler;
        int local_audio_port;
        int local_video_port;
        int remote_audio_port;
        int remote_video_port;
        public bool in_call = false;

        public SipMessage incoming_call = null;
        private SipMessage outgoing_invite = null;

        public event EventHandler StateChanged = null;

        private void OnStateChanged(CallState state)
        {
            if (this.StateChanged != null)
            {
                this.StateChanged(this, new EventArgs());
            }
        }

        public Call_Handler(ClientSipStack Stack, Preferences Settings, Multimedia_Handler Media_handler)
        {
            stack = Stack;
            settings = Settings;
            media_handler = Media_handler;
        }

        public void Start_Call(string to_uri, bool video_enabled, int local_audio_port, int local_video_port)
        {
            in_call = true;
            SipMessage request = new SipMessage();

            request.set_request_line("INVITE", to_uri);
            request.headers["From"] = SipUtilities.sip_tag(settings.ims_private_user_identity) + ";tag=" + SipUtilities.CreateTag();
            request.headers["To"] = SipUtilities.sip_tag(to_uri.Replace("sip:", ""));
            request.headers["CSeq"] = "1" + " INVITE";
            request.headers["Content-Type"] = "application/sdp";

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

            request.message_body = sb.ToString();
            outgoing_invite = request;
            stack.SendMessage(request);
        }


        public void Receive_Call()
        {
            if (!(in_call) && (incoming_call != null))
            {
                string remote_sdp = "";
                string remote_ip = "not_found";
                bool video_enabled = false;
                if (incoming_call.headers.ContainsKey("Content-Type") && incoming_call.headers["Content-Type"].ToLower().Contains("application/sdp"))
                {
                    remote_sdp = incoming_call.message_body;

                    foreach (string line in remote_sdp.Split('\n'))
                    {
                        if (line.ToLower().StartsWith("c="))
                        {
                            string[] c_line = line.Split();
                            remote_ip = c_line[2];
                        }

                        if (line.ToLower().StartsWith("m=audio"))
                        {
                            string[] m_audio_line = line.Split();
                            remote_audio_port = Int32.Parse(m_audio_line[1]);
                        }

                        if (line.ToLower().StartsWith("m=video"))
                        {
                            string[] m_video_line = line.Split();
                            remote_video_port = Int32.Parse(m_video_line[1]);
                            video_enabled = true;
                        }
                    }
                }

                in_call = true;
                SipMessage reply = stack.CreateResponse(SipResponseCodes.x200_Ok, incoming_call);
                reply.headers["Content-Type"] = "application/sdp";


                StringBuilder sb = new StringBuilder();
                sb.Append("v=0\n");
                sb.Append("o=- 0 0 IN IP4 " + settings.ims_ip_address + "\n");
                sb.Append("s=IMS Call\n");
                sb.Append("c=IN IP4 " + settings.ims_ip_address + "\n");
                sb.Append("t=0 0\n");
                sb.Append("m=audio " + settings.audiocall_local_port + " RTP/AVP 3 0 101\n");
                sb.Append("b=AS:64\n");
                sb.Append("a=rtpmap:3 GSM/8000\n");
                sb.Append("a=rtpmap:0 PCMU/8000\n");
                sb.Append("a=rtpmap:101 telephone-event/8000\n");
                sb.Append("a=fmtp:101 0-11\n");
                if (video_enabled)
                {
                    sb.Append("m=video " + settings.videocall_local_port + " RTP/AVP 96\n");
                    sb.Append("b=AS:128\n");
                    sb.Append("a=rtpmap:96 H263-1998\n");
                    sb.Append("a=fmtp:96 profile-level-id=0\n");
                }
                reply.message_body = sb.ToString();
                stack.SendMessage(reply);
                in_call = true;
                call_state = SipStack.CallState.Active;

                media_handler.Start_Audio_Rx(settings.audiocall_local_port, 8);
                media_handler.Start_Audio_Tx(remote_ip, remote_audio_port, 8);

                if (video_enabled)
                {
                    media_handler.Start_Video_Tx(remote_ip, remote_video_port);
                    media_handler.Start_Video_Rx(settings.videocall_local_port,SipUtilities.de_tag(SipUtilities.GetSipUri(incoming_call.headers["From"])));
                }


            }
        }

        public void process_Response(SipMessage message)
        {
            string remote_sdp;
            string remote_ip = "not_found";

            if (message.status_code_type == StatusCodes.Informational)
            {
               
            }
            else if (message.status_code_type == StatusCodes.Successful)
            {

                if ((this.call_state == CallState.Ringing) || (this.call_state == CallState.Calling))
                {
                    SetState(CallState.Active);
                    SipMessage request = stack.CreateAck(outgoing_invite);
                    stack.SendMessage(request);

                    bool video_enabled = false;
                    if (message.headers.ContainsKey("Content-Type") && message.headers["Content-Type"].ToLower().Contains("application/sdp"))
                    {
                        remote_sdp = message.message_body;
                        foreach (string line in remote_sdp.Split('\n'))
                        {
                            if (line.ToLower().StartsWith("c="))
                            {
                                string[] c_line = line.Split();
                                remote_ip = c_line[2];
                            }

                            if (line.ToLower().StartsWith("m=audio"))
                            {
                                string[] m_audio_line = line.Split();
                                remote_audio_port = Int32.Parse(m_audio_line[1]);
                            }

                            if (line.ToLower().StartsWith("m=video"))
                            {
                                string[] m_video_line = line.Split();
                                remote_video_port = Int32.Parse(m_video_line[1]);
                                video_enabled = true;
                            }
                        }
                    }

                    media_handler.Start_Audio_Rx(settings.audiocall_local_port, 8);
                    media_handler.Start_Audio_Tx(remote_ip, remote_audio_port, 8);
                    if (video_enabled)
                    {
                        media_handler.Start_Video_Rx(settings.videocall_local_port,SipUtilities.de_tag(SipUtilities.GetSipUri(outgoing_invite.headers["To"])));
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
                if (message.status_code == 603)
                {
                    SetState(CallState.Ending);
                    incoming_call = null;
                    outgoing_invite = null;
                    in_call = false;
                }
            }
        }

        void cancel_ResponseReceived(object sender, SipMessage message)
        {
            if (message.status_code_type == StatusCodes.ClientFailure)
            {
                SetState(CallState.Ended);
                SipMessage request = stack.CreateAck(incoming_call);
                stack.SendMessage(request);
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
            
            SetState(CallState.Ended);
            string uri = "";
            SipMessage temp = null;
            if (incoming_call !=null)
            {
                temp = incoming_call;
                uri = SipUtilities.de_tag(SipUtilities.GetSipUri(outgoing_invite.headers["From"]));
               
            }
            else if (outgoing_invite != null)
            {
                temp = outgoing_invite;
                uri = SipUtilities.de_tag(SipUtilities.GetSipUri(outgoing_invite.request_line));
            }
            SipMessage request = new SipMessage();
            request.set_request_line("BYE", uri);
            request.headers["From"] = temp.headers["From"];
            request.headers["To"] = temp.headers["To"];
            request.headers["Call-ID"] = temp.headers["Call-ID"];
            stack.SendMessage(request);
            incoming_call = null;
            outgoing_invite = null;
            in_call = false;

            media_handler.Stop_Audio_Rx();
            media_handler.Stop_Audio_Tx();
            media_handler.Stop_Video_Rx();
            media_handler.Stop_Video_Tx();
        }

        internal void Cancel_call(SipMessage e)
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
