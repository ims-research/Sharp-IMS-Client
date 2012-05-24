using System.Collections.Generic;

namespace IMS_client
{
    public class AddressBook
    {
        public List<Contact> Entries;
        
        public AddressBook()
        {
            Entries = new List<Contact>();
        }
    }
}
