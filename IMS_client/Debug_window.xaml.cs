using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Net;

namespace IMS_client
{
    /// <summary>
    /// Interaction logic for debug_window.xaml
    /// </summary>
    public partial class Debug_window : Window
    {
        public Debug_window()
        {
            InitializeComponent();
        }

        private void Debug_sip_msg_listbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug_sip_msg_textbox.Clear();
            if (Debug_sip_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Debug_sip_msg_textbox.Text = (Debug_sip_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }
        }

        private void Debug_http_msg_listbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug_http_msg_textbox.Clear();
            if (Debug_http_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Debug_http_msg_textbox.Text = (Debug_http_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }
        }

        public void Add_Sip_Response_Message(string status_code, string message)
        {
            ListBoxItem lbi = new ListBoxItem();
            lbi.Content = status_code;
            lbi.Tag = message;
            lbi.ToolTip = message;
            Debug_sip_msg_listbox.Items.Add(lbi);
        }

        public void Add_RAW_Message(string data)
        {
            if (data.Length > 0)
            {
                int min = Math.Min(data.Length, 3);
                ListBoxItem lbi = new ListBoxItem();
                lbi.Content = data.Substring(0, min) + "...";
                lbi.Tag = data;
                lbi.ToolTip = data;
                Raw_msg_listbox.Items.Add(lbi);
            }
        }

        public void Add_Sip_Request_Message(string method, string message)
        {
            ListBoxItem lbi = new ListBoxItem();
            lbi.Content = method;
            lbi.Tag = message;
            lbi.ToolTip = message;
            Debug_sip_msg_listbox.Items.Add(lbi);
        }

        public void Add_Http_Response_Message(HttpWebResponseEventArgs e)
        {
            HttpWebResponse response = e.response;
            ListBoxItem lbi = new ListBoxItem();
            lbi.Content = "Response";

            string headers = "";
            headers += "Response to " + response.Method + " " + response.ResponseUri + "\n";
            foreach (string key in response.Headers)
            {
                headers += key + ":" + response.Headers[key] + "\n";
            }
            lbi.Tag = headers + e.content;
            lbi.ToolTip = headers + e.content;
            Debug_http_msg_listbox.Items.Add(lbi);
        }

        public void Add_Http_Request_Message(HttpRequestEventArgs e)
        {
            HttpWebRequest request = e.request;
            ListBoxItem lbi = new ListBoxItem();
            string headers = "";

            headers += request.Method + " " + request.RequestUri + "\n";

            foreach (string key in request.Headers)
            {
                headers += key + ":" + request.Headers[key] + "\n";
            }
            lbi.Content = request.Method;
            lbi.Tag = headers + e.content;
            lbi.ToolTip = headers + e.content;
            Debug_http_msg_listbox.Items.Add(lbi);
        }

        private void Debug_gst_msg_listbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug_gst_msg_textbox.Clear();
            if (Debug_gst_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Debug_gst_msg_textbox.Text = (Debug_gst_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }
        }

        public void Add_Gst_Message(string type, string message)
        {
            ListBoxItem lbi = new ListBoxItem();
            lbi.Content = type;
            lbi.Tag = message;
            lbi.ToolTip = message;
            Debug_gst_msg_listbox.Items.Add(lbi);
        }

        private void Raw_msg_listbox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Raw_msg_textbox.Clear();
            if (Raw_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Raw_msg_textbox.Text = (Raw_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }

        }
    }
}
