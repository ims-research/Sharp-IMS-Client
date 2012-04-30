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
            this.app.presenceUA.remoteParty = new Address(sip_uri);
            this.app.presenceUA.localParty = this.app.registerUA.localParty;
            Message request = this.app.presenceUA.createRequest("SUBSCRIBE");
            request.insertHeader(new Header("presence", "Event"));
            this.app.presenceUA.sendRequest(request);
        }
       
        public void Process_Request(Message request)
        {
            if (request.method.ToUpper().Contains("NOTIFY"))
            {
                if (request.headers.ContainsKey("Content-Length"))
                {
                    if (request.body.Length > 0)
                    {
                        try
                        {
                            XDocument x_doc = XDocument.Parse(request.body.Trim());
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
                                this.Presence_Changed_Event(this, new PresenceChangedArgs(request.first("From").value.ToString(), basic, note));
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
            this.app.publish(sip_uri, basic, note, expires);
        }
    }
}
