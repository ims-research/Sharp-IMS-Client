using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IMS_client
{
    public class Address_Book
    {
        public List<Contact> entries;
        
        public Address_Book()
        {
            entries = new List<Contact>();
        }
    }
}
