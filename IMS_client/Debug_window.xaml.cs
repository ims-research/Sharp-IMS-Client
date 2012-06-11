using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Net;
using System.Windows.Documents;
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

                int min = Math.Min(20, data.IndexOf("\r"));
                string dots = "";
                if (min == 20) dots = "...";
                ListBoxItem lbi = new ListBoxItem { Content = data.Substring(0, min) + dots, Tag = data, ToolTip = data, Background = GetBrushColor(sent) };
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
            Raw_msg_textbox.Document.Blocks.Clear();
            if (Raw_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                FlowDocument flow = new FlowDocument();
                Paragraph para = new Paragraph();
                para.Inlines.Add(new Run((Raw_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString()));
                flow.Blocks.Add(para);
                Raw_msg_textbox.Document = flow;
                HighlightFoundTerm(RawMsgSearchBox.Text, Raw_msg_textbox);
            }

        }

        private void HighlightFoundTerm(string searchTerm, RichTextBox rawMsgTextbox)
        {
            List<TextRange> myRanges = FindWordFromPosition(rawMsgTextbox.Document.ContentStart, searchTerm);
            if (myRanges.Count > 0)
            {
                foreach (TextRange textRange in myRanges)
                {
                    textRange.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(Colors.Yellow));
                    textRange.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);
                }
            }
        }

        List<TextRange> FindWordFromPosition(TextPointer position, string word)
        {
            List<TextRange> ranges = new List<TextRange>();
            while (position != null)
            {
                if (position.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
                {
                    string textRun = position.GetTextInRun(LogicalDirection.Forward);

                    // Find the starting index of any substring that matches "word".
                    int indexInRun = textRun.IndexOf(word);
                    if (indexInRun >= 0)
                    {
                        TextPointer start = position.GetPositionAtOffset(indexInRun);
                        TextPointer end = start.GetPositionAtOffset(word.Length);
                        ranges.Add(new TextRange(start, end));
                    }
                }
                position = position.GetNextContextPosition(LogicalDirection.Forward);
            }
            return ranges;
        }

        private void RawMsgSearchBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            Raw_msg_textbox.Document.Blocks.Clear();
            if (Raw_msg_listbox.SelectedItem as ListBoxItem != null)
            {
                FlowDocument flow = new FlowDocument();
                Paragraph para = new Paragraph();
                para.Inlines.Add(new Run((Raw_msg_listbox.SelectedItem as ListBoxItem).Tag.ToString()));
                flow.Blocks.Add(para);
                Raw_msg_textbox.Document = flow;
                HighlightFoundTerm(RawMsgSearchBox.Text, Raw_msg_textbox);
            }
        }
    }
}
