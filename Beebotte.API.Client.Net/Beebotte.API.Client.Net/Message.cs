using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Beebotte.API.Client.Net
{
    public class Message
    {
        public string channel { get; set; }
        public string resource { get; set; }
        public object data { get; set; }
        public string eid { get; set; }
        public int sid { get; set; }
    }
}
