using System;
using System.IO;
using System.Net;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Linq;
namespace IMS_client
{
    public class HttpRequestEventArgs : EventArgs
    {
        public HttpWebRequest request;
        public string content;

        public HttpRequestEventArgs(HttpWebRequest Request, string Content)
        {
            this.request = Request;
            content = Content;
        }
    }

    public class HttpWebResponseEventArgs : EventArgs
    {
        public HttpWebResponse response;
        public string content;

        public HttpWebResponseEventArgs(HttpWebResponse Response, string Content)
        {
            this.response = Response;
            content = Content;
        }
    }

    public class XDMS_handler
    {
        public event EventHandler<HttpWebResponseEventArgs> Response_Log_Event;
        public event EventHandler<HttpRequestEventArgs> Request_Log_Event;
        string user_name { get; set; }
        string password { get; set; }
        string server_name { get; set; }
        int server_port { get; set; }
        string realm { get; set; }



        public XDMS_handler(string User_name, string Password, string Server_name, int Port, string Realm)
        {
            user_name = User_name;
            password = Password;
            server_name = Server_name;
            server_port = Port;
            realm = Realm;
        }

        public void Store_File(string xml_doc_name, string xml)
        {

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(new Uri("http://" + server_name + ":" + server_port), "/xcap-root/test-app/users/" + "sip:" + user_name + "@" + realm + "/Resources/" + xml_doc_name));
                request.Method = "PUT";
                request.Credentials = request.Credentials = new NetworkCredential(user_name, password);
                request.ContentType = "application/test-app+xml";

                StreamWriter writer = new StreamWriter(request.GetRequestStream());

                writer.WriteLine(xml);
                writer.Close();

             if (this.Request_Log_Event!= null)
            {
                this.Request_Log_Event(this,new HttpRequestEventArgs(request,XDocument.Parse(xml).ToString()));
            }

             HttpWebResponse response = (HttpWebResponse)request.GetResponse();

             Stream resStream = response.GetResponseStream();
            
                 StringBuilder sb = new StringBuilder();

                // used on each read operation
            byte[] buf = new byte[8192];

             string tempString = null;
             int count = 0;

             do
             {
                 // fill the buffer with data
                 count = resStream.Read(buf, 0, buf.Length);

                 // make sure we read some data
                 if (count != 0)
                 {
                     // translate from bytes to ASCII text
                     tempString = Encoding.ASCII.GetString(buf, 0, count);

                     // continue building the string
                     sb.Append(tempString);
                 }
             }
             while (count > 0); // any more data to read?


                if (this.Response_Log_Event != null)
                {
                    this.Response_Log_Event(this, new HttpWebResponseEventArgs(response,sb.ToString()));
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error storing xml file. " + e.Message.ToString());
            }
        }

        public XDocument Retrieve_File(string xml_doc_name)
        {
            XDocument xml_document = new XDocument();
            XmlWriter writer = xml_document.CreateWriter();

            try
            {

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(new Uri("http://" + server_name + ":" + server_port), "/xcap-root/test-app/users/" + "sip:" + user_name + "@" + realm + "/" + xml_doc_name));
                request.Method = "GET";
                request.Credentials = request.Credentials = new NetworkCredential(user_name, password);
                request.ContentType = "application/test-app+xml";

                XmlWriter xml_writer = xml_document.CreateWriter();

                // used to build entire input
                StringBuilder sb = new StringBuilder();

                // used on each read operation
                byte[] buf = new byte[8192];

                if (this.Request_Log_Event != null)
                {
                    this.Request_Log_Event(this, new HttpRequestEventArgs(request,"No Content"));
                }


                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                
                Stream resStream = response.GetResponseStream();

                string tempString = null;
                int count = 0;

                do
                {
                    // fill the buffer with data
                    count = resStream.Read(buf, 0, buf.Length);

                    // make sure we read some data
                    if (count != 0)
                    {
                        // translate from bytes to ASCII text
                        tempString = Encoding.ASCII.GetString(buf, 0, count);

                        // continue building the string
                        sb.Append(tempString);
                    }
                }
                while (count > 0); // any more data to read?

                xml_document = XDocument.Parse(sb.ToString());

                if (this.Response_Log_Event != null)
                {
                    this.Response_Log_Event(this, new HttpWebResponseEventArgs(response,xml_document.ToString()));
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error retrieving network stored address book - check XDMS settings. Error:" + e.Message.ToString());
            }
            return xml_document;
        }
    }
}
