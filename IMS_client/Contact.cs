using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;

namespace IMS_client
{
    public class Contact
    {
        public string Name { get; set; }
        public string Nickname { get; set; }
        public string Sip_URI { get; set; }
        public string Tel_URI { get; set; }
        public string Email_Address { get; set; }
        public string Group { get; set; }

        [XmlIgnore]
        private Status _status { get; set; }

        public Status Get_Status()
        {
            return _status;
            
        }

        public void Set_Status(Status status)
        {
            _status.basic = status.basic;
            _status.note = status.note;
            _status.display_name = status.display_name;
        }


        public Contact()
        {
            this.Name = "Enter_Name";
            this.Nickname = "Enter_Nickname";
            this.Sip_URI = "Enter_Sip_URI";
            this.Tel_URI = "Enter_Tel_URI";
            this.Email_Address = "Email_Address";
            this.Group = "Enter_Group";

            _status = new Status();
        }

        public Contact(Contact to_clone)
        {
            this.Name = to_clone.Name;
            this.Nickname = to_clone.Nickname;
            this.Sip_URI = to_clone.Sip_URI;
            this.Tel_URI = to_clone.Tel_URI;
            this.Email_Address = to_clone.Email_Address;
            this.Group = to_clone.Group;
            _status = new Status();
            this.Set_Status(to_clone.Get_Status());
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
}
