using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Beebotte.API.Client.Net
{
    public class Subscription
    {
        private string channelInternalName;
        public Subscription(string channel, string resource, bool isPrivate, bool read, bool write, bool presence)
        {
            this.Channel = channel;
            this.Resource = resource;
            this.Private = isPrivate;
            this.Read = read;
            this.Write = write;
            this.Presence = presence;
        }

        public string Channel { get; set; }
        public string Resource { get; set; }
        public bool Private { get; set; }
        public bool Read { get; set; }
        public bool Write { get; set; }
        public bool Presence { get; set; }
        public string UserId { get; set; }
        public string ChannelInternalName
        {
            get
            {
                if (String.IsNullOrEmpty(channelInternalName))
                {
                    channelInternalName = Private ? String.Format("private-{0}", Channel) : Channel;
                    channelInternalName = Presence ? String.Format("presence-{0}", channelInternalName) : channelInternalName;
                }
                return channelInternalName;
            }
        }

        public event EventHandler<EventArgs<Message>> OnMessage;
        internal void MessageReceived(EventArgs<Message> e)
        {
            EventHandler<EventArgs<Message>> handler = OnMessage;
            if (handler != null)
            {
                handler(this, e);
            }
        }
    }
}
