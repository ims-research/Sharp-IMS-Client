using System;
using System.Text;
using System.Windows;
using SIPLib.SIP;

namespace IMS_client
{
    public class IMHandler
    {

        public class MessageReceivedArgs : EventArgs
        {
            public string Contact;
            public string Message;

            public MessageReceivedArgs(string contact, string message)
            {
                Contact = contact;
                Message = message;
            }
        }

        public class TypingMessageRecievedArgs : EventArgs
        {
            public string Contact;
            public string Message;

            public TypingMessageRecievedArgs(string contact, string message)
            {
                Contact = contact;
                Message = message;
            }
        }
        
        public event EventHandler<MessageReceivedArgs> MessageRecievedEvent;
        public event EventHandler<TypingMessageRecievedArgs> TypingMessageRecievedEvent;

        readonly SIPApp _app;

        public IMHandler(SIPApp app)
        {
            _app = app;
        }

        public void SendMessage(string sipUri, string message)
        {
            _app.SendIM(sipUri, message);
            //TODO Check Sending of IMs
            //Message request = new Message();
            //request.set_request_line("MESSAGE", sip_uri);
            //request.headers["From"] = SipUtilities.sip_tag(settings.ims_private_user_identity) + ";tag=" + SipUtilities.CreateTag();
            //request.headers["To"] = SipUtilities.sip_tag(sip_uri.Replace("sip:", ""));
            //request.headers["CSeq"] = "5" + " MESSAGE";
            //request.headers["Content-Type"] = "text/plain";
            //request.message_body = message;
            //stack.SendMessage(request);
        }

        public void ProcessMessage(Message request)
        {
            if (request.First("Content-Type").ToString().ToUpper().Contains("TEXT/PLAIN"))
            {
                try
                {
                    if (MessageRecievedEvent != null)
                    {
                        MessageRecievedEvent(this, new MessageReceivedArgs(request.First("From").Value.ToString(), request.Body));
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Error in handling IM Message : " + exception.Message);
                }
            }
            else if (request.First("Content-Type").ToString().ToUpper().Equals("APPLICATION/IM-ISCOMPOSING+XML"))
            {
                try
                {
                    if (TypingMessageRecievedEvent != null)
                    {
                        TypingMessageRecievedEvent(this, new TypingMessageRecievedArgs(request.First("From").Value.ToString(), request.Body));
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Error in handling IM Message : " + exception.Message);
                }

            }

        }

        public void SendTypingNotice(string sipUri)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<isComposing xmlns=\"urn:ietf:params:xml:ns:im-iscomposing\"\n");
            sb.Append("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n");
            sb.Append("xsi:schemaLocation=\"urn:ietf:params:xml:ns:im-composing iscomposing.xsd\">\n");
            sb.Append("<state>active</state>\n");
            sb.Append("<contenttype>text/plain</contenttype>\n");
            sb.Append("</isComposing>");
            string messageBody = sb.ToString();
            _app.SendIM(sipUri, messageBody,"application/im-iscomposing+xml");
        }
    }
}
