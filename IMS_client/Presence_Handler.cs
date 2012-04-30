using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using SIPLib;

namespace IMS_client
{
    class Presence_Handler
    {

        public class PresenceChangedArgs : EventArgs
        {
            public string contact;
            public string basis;
            public string note;

            public PresenceChangedArgs(string Contact, string Basis, string Note)
            {
                contact = Contact;
                basis = Basis;
                note = Note;
            }
        }

        SIPApp app;
        Preferences settings;

        public event EventHandler<SipMessageEventArgs> Presence_Response_Event;
        public event EventHandler<PresenceChangedArgs> Presence_Changed_Event;

        public Presence_Handler(SIPApp app, Preferences Settings)
        {
            this.app = app;
            settings = Settings;
        }

        public void Subscribe(string sip_uri)
        {

            Message request = new Message();
            request.set_request_line("SUBSCRIBE", sip_uri);
            request.headers["Event"] = "presence";
            request.headers["CSeq"] = "1" + " SUBSCRIBE";
            request.headers["To"] = SipUtilities.sip_tag(sip_uri.Replace("sip:",""));
            request.headers["From"] = SipUtilities.sip_tag(settings.ims_private_user_identity) + ";tag=" + SipUtilities.CreateTag();
            stack.SendMessage(request);
        }
        /*
         * 


         */

        public void Process_Request(Message request)
        {
            if (request.method.ToUpper().Contains("NOTIFY"))
            {
                Message reply = stack.CreateResponse(SipResponseCodes.x200_Ok, request);
                stack.SendMessage(reply);
                if (request.headers.ContainsKey("Content-Length"))
                {
                    if (int.Parse(request.headers["Content-Length"]) != 0)
                    {
                        try
                        {
                            XDocument x_doc = XDocument.Parse(request.message_body.Trim());
                            XName xname = x_doc.Root.Name;
                            string basic = "";
                            string note = "";
                            foreach (XElement x_element in x_doc.Descendants())
                            {
                                switch (x_element.Name.ToString())
                                {
                                    case "{urn:ietf:params:xml:ns:pidf}basic":
                                        basic = x_element.Value;
                                        break;

                                    case "{urn:ietf:params:xml:ns:pidf}note":
                                        note = x_element.Value;
                                        break;
                                    default:
                                        break;
                                }
                            }

                            if (this.Presence_Changed_Event != null)
                            {
                                this.Presence_Changed_Event(this, new PresenceChangedArgs(SipUtilities.GetSipUri(request.headers["From"]), basic, note));
                            }
                        }
                        catch (Exception exception)
                        {
                            MessageBox.Show("Error in handling presence xml: " + exception.Message);
                        }
                    }

                }

            }
        }

        public void Publish(string sip_uri, string basic, string note,int expires)
        {
            SipMessage request = new SipMessage();
            request.set_request_line("PUBLISH", sip_uri);
            request.headers["Event"] = "presence";
            request.headers["P-Preferred-Identity"] = "<" + settings.ims_public_user_identity + ">";
            request.headers["From"] = SipUtilities.sip_tag(settings.ims_private_user_identity) + ";tag=" + SipUtilities.CreateTag();
            request.headers["To"] = SipUtilities.sip_tag(sip_uri.Replace("sip:", ""));
            request.headers["CSeq"] = "21" + " PUBLISH";
            request.headers["Content-Type"] = "application/pidf+xml";
            
            StringBuilder sb = new StringBuilder();
            sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
            sb.Append("<presence xmlns=\"urn:ietf:params:xml:ns:pidf\" xmlns:im=\"urn:ietf:params:xml:ns:pidf:im\" entity=\"" + sip_uri + "\">\n");
            sb.Append("<tuple id=\"Sharp_IMS_Client\">\n");
            sb.Append("<status>\n");
            sb.Append("<basic>" + basic + "</basic>\n");
            sb.Append("</status>\n");
            sb.Append("<note>" + note + "</note>\n");
            sb.Append("</tuple>\n");
            sb.Append("</presence>\n");
            request.message_body = sb.ToString();
            stack.SendMessage(request);
            
        }
    }
}
