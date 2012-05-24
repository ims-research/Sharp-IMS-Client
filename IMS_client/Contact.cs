using System.Xml.Serialization;

namespace IMS_client
{
    public class Contact
    {
        public string Name { get; set; }
        public string Nickname { get; set; }
        public string SipUri { get; set; }
        public string TelUri { get; set; }
        public string EmailAddress { get; set; }
        public string Group { get; set; }

        [XmlIgnore]
        private Status Status { get; set; }

        public Status GetStatus()
        {
            return Status;
            
        }

        public void SetStatus(Status status)
        {
            Status.basic = status.basic;
            Status.note = status.note;
            Status.display_name = status.display_name;
        }


        public Contact()
        {
            Name = "Enter_Name";
            Nickname = "Enter_Nickname";
            SipUri = "Enter_Sip_URI";
            TelUri = "Enter_Tel_URI";
            EmailAddress = "Email_Address";
            Group = "Enter_Group";

            Status = new Status();
        }

        public Contact(Contact toClone)
        {
            Name = toClone.Name;
            Nickname = toClone.Nickname;
            SipUri = toClone.SipUri;
            TelUri = toClone.TelUri;
            EmailAddress = toClone.EmailAddress;
            Group = toClone.Group;
            Status = new Status();
            SetStatus(toClone.GetStatus());
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
