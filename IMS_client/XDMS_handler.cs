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
        public HttpWebRequest Request;
        public string Content;

        public HttpRequestEventArgs(HttpWebRequest request, string content)
        {
            Request = request;
            Content = content;
        }
    }

    public class HttpWebResponseEventArgs : EventArgs
    {
        public HttpWebResponse Response;
        public string Content;

        public HttpWebResponseEventArgs(HttpWebResponse response, string content)
        {
            Response = response;
            Content = content;
        }
    }

    public class XdmsHandler
    {
        public event EventHandler<HttpWebResponseEventArgs> ResponseLogEvent;
        public event EventHandler<HttpRequestEventArgs> RequestLogEvent;
        string UserName { get; set; }
        string Password { get; set; }
        string ServerName { get; set; }
        int ServerPort { get; set; }
        string Realm { get; set; }



        public XdmsHandler(string userName, string password, string serverName, int port, string realm)
        {
            UserName = userName;
            Password = password;
            ServerName = serverName;
            ServerPort = port;
            Realm = realm;
        }

        public void StoreFile(string xmlDocName, string xml)
        {

            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(new Uri("http://" + ServerName + ":" + ServerPort), "/xcap-root/test-app/users/" + "sip:" + UserName + "@" + Realm + "/Resources/" + xmlDocName));
                request.Method = "PUT";
                request.Credentials = request.Credentials = new NetworkCredential(UserName, Password);
                request.ContentType = "application/test-app+xml";

                StreamWriter writer = new StreamWriter(request.GetRequestStream());

                writer.WriteLine(xml);
                writer.Close();

             if (RequestLogEvent!= null)
            {
                RequestLogEvent(this,new HttpRequestEventArgs(request,XDocument.Parse(xml).ToString()));
            }

             HttpWebResponse response = (HttpWebResponse)request.GetResponse();

             Stream resStream = response.GetResponseStream();
            
                 StringBuilder sb = new StringBuilder();

                // used on each read operation
            byte[] buf = new byte[8192];

                int count = 0;

             do
             {
                 // fill the buffer with data
                 if (resStream != null) count = resStream.Read(buf, 0, buf.Length);

                 // make sure we read some data
                 if (count != 0)
                 {
                     // translate from bytes to ASCII text
                     string tempString = Encoding.ASCII.GetString(buf, 0, count);

                     // continue building the string
                     sb.Append(tempString);
                 }
             } while (count > 0); // any more data to read?


                if (ResponseLogEvent != null)
                {
                    ResponseLogEvent(this, new HttpWebResponseEventArgs(response,sb.ToString()));
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error storing xml file. " + e.Message.ToString());
            }
        }

        public XDocument RetrieveFile(string xmlDocName)
        {
            XDocument xmlDocument = new XDocument();
            try
            {

                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri(new Uri("http://" + ServerName + ":" + ServerPort), "/xcap-root/test-app/users/" + "sip:" + UserName + "@" + Realm + "/" + xmlDocName));
                request.Method = "GET";
                request.Credentials = request.Credentials = new NetworkCredential(UserName, Password);
                request.ContentType = "application/test-app+xml";

                // used to build entire input
                StringBuilder sb = new StringBuilder();

                // used on each read operation
                byte[] buf = new byte[8192];

                if (RequestLogEvent != null)
                {
                    RequestLogEvent(this, new HttpRequestEventArgs(request,"No Content"));
                }


                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                
                Stream resStream = response.GetResponseStream();

                int count = 0;

                do
                {
                    // fill the buffer with data
                    if (resStream != null) count = resStream.Read(buf, 0, buf.Length);

                    // make sure we read some data
                    if (count != 0)
                    {
                        // translate from bytes to ASCII text
                        string tempString = Encoding.ASCII.GetString(buf, 0, count);

                        // continue building the string
                        sb.Append(tempString);
                    }
                } while (count > 0); // any more data to read?

                xmlDocument = XDocument.Parse(sb.ToString());

                if (ResponseLogEvent != null)
                {
                    ResponseLogEvent(this, new HttpWebResponseEventArgs(response,xmlDocument.ToString()));
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("Error retrieving network stored address book - check XDMS settings. Error:" + e.Message.ToString());
            }
            return xmlDocument;
        }
    }
}
