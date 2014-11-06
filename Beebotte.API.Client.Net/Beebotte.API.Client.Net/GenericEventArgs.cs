using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Beebotte.API.Client.Net
{
    public class EventArgs<T> : EventArgs
    {
        public T Message { get; set; }

        public EventArgs(T input)
        {
            Message = input;
        }
    }
}
