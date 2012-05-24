using System.ComponentModel;

namespace IMS_client
{
    public class Status : INotifyPropertyChanged
    {
        private string _basic;
        private string _note;
        private string _displayName;

        public string Basic { get { return _basic; } set { 
            _basic = value;
            OnPropertyChanged("basic"); } }
        public string Note
        {
            get { return _note; }
            set
            {
                _note = value;
                OnPropertyChanged("note");
            }
        }
        public string DisplayName
        {
            get { return _displayName; }
            set
            {
                _displayName = value;
                OnPropertyChanged("display_name");
            }
        }

        public Status()
        {
            Basic="nothing_yet ";
            Note = "nothing_yet ";
            DisplayName = "nothing_yet ";

        }

        public  Status(string basic, string note,string displayName)
        {
            Basic = basic;
            Note = note;
            DisplayName = displayName;
        }

        public event PropertyChangedEventHandler PropertyChanged;                
          protected void OnPropertyChanged(string propertyName)                    
          {
              if (PropertyChanged != null)                                    
                 PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
         }                                                                     
        
    }
}
