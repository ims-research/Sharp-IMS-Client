using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using System.Windows.Media;

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

        private void DebugSIPMsgListboxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug_sip_msg_textbox.Clear();
            if (Debug_sip_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Debug_sip_msg_textbox.Text = (Debug_sip_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }
        }

        private void DebugHttpMsgListboxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug_http_msg_textbox.Clear();
            if (Debug_http_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Debug_http_msg_textbox.Text = (Debug_http_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }
        }

        public void AddSipResponseMessage(int statusCode, string message, bool sent)
        {
            ListBoxItem lbi = new ListBoxItem { Content = statusCode.ToString(), Tag = message, ToolTip = message, Background = GetBrushColor(sent) };
            Debug_sip_msg_listbox.Items.Add(lbi);
        }

        public void AddRawMessage(string data,bool sent)
        {
            if (data.Trim().Length > 0)
            {
                int min = Math.Min(data.Length, 20);
                ListBoxItem lbi = new ListBoxItem { Content = data.Substring(0, min) + "...", Tag = data, ToolTip = data, Background = GetBrushColor(sent) };
                Raw_msg_listbox.Items.Add(lbi);
            }
        }

        private Brush GetBrushColor(bool sent)
        {
            return sent ? Brushes.LightBlue : Brushes.BurlyWood;
        }

        public void AddSipRequestMessage(string method, string message, bool sent)
        {
            ListBoxItem lbi = new ListBoxItem { Content = method, Tag = message, ToolTip = message, Background = GetBrushColor(sent) };
            Debug_sip_msg_listbox.Items.Add(lbi);
        }

        public void AddHttpResponseMessage(HttpWebResponseEventArgs e)
        {
            HttpWebResponse response = e.Response;
            ListBoxItem lbi = new ListBoxItem {Content = "Response"};

            string headers = "";
            headers += "Response to " + response.Method + " " + response.ResponseUri + "\n";
            headers = response.Headers.Cast<string>().Aggregate(headers, (current, key) => current + (key + ":" + response.Headers[key] + "\n"));
            lbi.Tag = headers + e.Content;
            lbi.ToolTip = headers + e.Content;
            Debug_http_msg_listbox.Items.Add(lbi);
        }

        public void AddHttpRequestMessage(HttpRequestEventArgs e)
        {
            HttpWebRequest request = e.Request;
            ListBoxItem lbi = new ListBoxItem();
            string headers = "";
            headers += request.Method + " " + request.RequestUri + "\n";
            headers = request.Headers.Cast<string>().Aggregate(headers, (current, key) => current + (key + ":" + request.Headers[key] + "\n"));
            lbi.Content = request.Method;
            lbi.Tag = headers + e.Content;
            lbi.ToolTip = headers + e.Content;
            Debug_http_msg_listbox.Items.Add(lbi);
        }

        private void DebugGstMsgListboxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Debug_gst_msg_textbox.Clear();
            if (Debug_gst_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Debug_gst_msg_textbox.Text = (Debug_gst_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }
        }

        public void AddGstMessage(string type, string message)
        {
            ListBoxItem lbi = new ListBoxItem {Content = type, Tag = message, ToolTip = message};
            Debug_gst_msg_listbox.Items.Add(lbi);
        }

        private void RawMsgListboxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Raw_msg_textbox.Clear();
            if (Raw_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                Raw_msg_textbox.Text = (Raw_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString();
            }

        }
    }
}
