using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Beebotte.API.Client.Net
{
    public class Subscription
    {
        private string channelInternalName;
        public Subscription(string channel, string resource, bool isPrivate, bool read, bool write)
        {
            this.Channel = channel;
            this.Resource = resource;
            this.Private = isPrivate;
            this.Read = read;
            this.Write = write;
        }
        public string Channel { get; set; }
        public string Resource { get; set; }
        public bool Private { get; set; }
        public bool Read { get; set; }
        public bool Write { get; set; }
        public string UserId { get; set; }
        public string ChannelInternalName
        {
            get
            {
                if (String.IsNullOrEmpty(channelInternalName))
                {
                    channelInternalName = Private ? String.Format("private-{0}", Channel) : Channel;
                }
                return channelInternalName;
            }
        }

        public event EventHandler<EventArgs<Message>> ResourceMessageReceived;
        internal void OnResourceMessageReceived(EventArgs<Message> e)
        {
            EventHandler<EventArgs<Message>> handler = ResourceMessageReceived;
            if (handler != null)
            {
                handler(this, e);
            }
        }

    }
}
