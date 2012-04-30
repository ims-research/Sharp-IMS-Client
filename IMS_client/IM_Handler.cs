using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using SIPLib;
namespace IMS_client
{
    public class IM_Handler
    {

        public class Message_Received_Args : EventArgs
        {
            public string contact;
            public string message;

            public Message_Received_Args(string Contact, string Message)
            {
                contact = Contact;
                message = Message;
            }
        }

        public class Typing_Message_Recieved_Args : EventArgs
        {
            public string contact;
            public string message;

            public Typing_Message_Recieved_Args(string Contact, string Message)
            {
                contact = Contact;
                message = Message;
            }
        }
        
        public event EventHandler<Message_Received_Args> Message_Recieved_Event;
        public event EventHandler<Typing_Message_Recieved_Args> Typing_Message_Recieved_Event;

        SIPApp app;
        Preferences settings;

        public IM_Handler(SIPApp app, Preferences Settings)
        {
            this.app = app;
            settings = Settings;
        }

        public void Send_Message(string sip_uri, string message)
        {
            this.app.SendIM(sip_uri, message);
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

        public void Process_Message(Message request)
        {
           

            if (request.first("Content-Type").ToString().ToUpper().Contains("TEXT/PLAIN"))
            {
                try
                {
                    if (this.Message_Recieved_Event != null)
                    {
                        this.Message_Recieved_Event(this, new Message_Received_Args(request.first("From").value.ToString(), request.body));
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Error in handling IM Message : " + exception.Message);
                }
            }
            else if (request.first("Content-Type").ToString().ToUpper().Equals("APPLICATION/IM-ISCOMPOSING+XML"))
            {
                try
                {
                    if (this.Typing_Message_Recieved_Event != null)
                    {
                        this.Typing_Message_Recieved_Event(this, new Typing_Message_Recieved_Args(request.first("From").value.ToString(), request.body));
                    }
                }
                catch (Exception exception)
                {
                    MessageBox.Show("Error in handling IM Message : " + exception.Message);
                }

            }

        }

        public void Send_Typing_Notice(string sip_uri)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<isComposing xmlns=\"urn:ietf:params:xml:ns:im-iscomposing\"\n");
            sb.Append("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"\n");
            sb.Append("xsi:schemaLocation=\"urn:ietf:params:xml:ns:im-composing iscomposing.xsd\">\n");
            sb.Append("<state>active</state>\n");
            sb.Append("<contenttype>text/plain</contenttype>\n");
            sb.Append("</isComposing>");
            string message_body = sb.ToString();
            this.app.SendIM(sip_uri, message_body,"application/im-iscomposing+xml");
        }
    }
}
