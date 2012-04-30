using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;

namespace IMS_client
{
    public class Status : INotifyPropertyChanged
    {
        private string _basic;
        private string _note;
        private string _display_name;

        public string basic { get { return _basic; } set { 
            _basic = value;
            OnPropertyChanged("basic"); } }
        public string note
        {
            get { return _note; }
            set
            {
                _note = value;
                OnPropertyChanged("note");
            }
        }
        public string display_name
        {
            get { return _display_name; }
            set
            {
                _display_name = value;
                OnPropertyChanged("display_name");
            }
        }

        public Status()
        {
            basic="nothing_yet ";
            note = "nothing_yet ";
            display_name = "nothing_yet ";

        }

        public  Status(string Basic, string Note,string Display_name)
        {
            basic = Basic;
            note = Note;
            display_name = Display_name;
        }

        public event PropertyChangedEventHandler PropertyChanged;                
          protected void OnPropertyChanged(string propertyName)                    
          {
              if (this.PropertyChanged != null)                                    
                 PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
         }                                                                     
        
    }
}
