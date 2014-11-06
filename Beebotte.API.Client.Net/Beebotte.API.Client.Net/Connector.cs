using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Quobject.SocketIoClientDotNet.Client;

namespace Beebotte.API.Client.Net
{
    public class Connector
    {
        #region Fields

        private string AccessKey;
        private string SecureKey;
        private string Sid;
        private Socket bbtSocket;
        private string Uri;
        private int APIVersion;
        private int Port;
        private List<Subscription> Subscriptions;

        #endregion

        #region Properties

        public bool Connected { get; set; }

        #endregion

        #region  ctor
        public Connector(string apiKey, string uri, int apiVersion = 1, int port = 80)
        {
            if (String.IsNullOrEmpty(apiKey) || String.IsNullOrEmpty(uri))
            {
                var errorMsg = String.Empty;
                errorMsg = String.IsNullOrEmpty(apiKey) ? "Missing API Key. " : errorMsg;
                errorMsg = String.IsNullOrEmpty(uri) ? String.Concat(errorMsg, "Missing Socket URI") : errorMsg;
                throw new KeyNotFoundException(errorMsg);
            }
            AccessKey = apiKey;
            Uri = uri;
            APIVersion =  apiVersion;
            Port = port;
        }

        public Connector(string apiKey, string securekey, string uri, int apiVersion = 1, int port = 80)
            : this(apiKey, uri, apiVersion)
        {
            if (String.IsNullOrEmpty(securekey))
                throw new KeyNotFoundException("Missing Secure Key");
            SecureKey = securekey;
        }

        #endregion

        #region Public Methods

        public void Connect()
        {
            var options = new IO.Options();
            options.Port = Port;
            options.QueryString = String.Format("key={0}", AccessKey);
            bbtSocket = IO.Socket(Uri, options);
            bbtSocket.On("message", (data) =>
            {
                OnMessageReceived(new EventArgs<Message>(JsonConvert.DeserializeObject<Message>(data.ToString())));
            });

            bbtSocket.On(Socket.EVENT_CONNECT, () =>
            {
                Connected = true;
                bbtSocket.Emit("getsid");
            });

            bbtSocket.On("getsid", (data) =>
            {
                if (String.IsNullOrEmpty(Sid))
                {
                    Sid = data.ToString();
                    OnSocketConnected(EventArgs.Empty);
                }
            });

            bbtSocket.On(Socket.EVENT_CONNECT_ERROR, (u) =>
            {
                OnConnectionFailed(new EventArgs<string>(String.Format("Unable to connect. Error Message:{0}", u)));
            });

            bbtSocket.On(Socket.EVENT_ERROR, (u) =>
            {
                OnConnectionFailed(new EventArgs<string>(String.Format("An error has occured. {0}", u)));
            });

            if (Subscriptions != null && Subscriptions.Count > 0)
            {
                foreach (var subscription in Subscriptions)
                {
                    SendSubscription(subscription);
                }
            }

        }
        public void Subscribe(string channel, string resource, bool isPrivate, bool read, bool write)
        {
            if (Subscriptions == null)
                Subscriptions = new List<Subscription>();
            var subscription = new Subscription(channel, resource, isPrivate, read, write);
            Subscriptions.Add(subscription);
            SendSubscription(subscription);
        }
        public void Unsubscribe(string channel, string resource, bool isPrivate)
        {
            var removedSubsciptions = from subs in Subscriptions where subs.Channel.Equals(channel) && subs.Resource.Equals(resource) select subs;
            if (removedSubsciptions != null && Subscriptions.Count > 0)
            {
                foreach (var subs in removedSubsciptions)
                {
                    Subscriptions.Remove(subs);
                }
            }
            var unsubscribeMessage = new JObject();
            var messageData = new JObject();
            var channelName = isPrivate ? String.Format("private-{0}", channel) : channel;
            messageData.Add("channel", channelName);
            messageData.Add("resource", resource);
            unsubscribeMessage.Add("version", APIVersion);
            unsubscribeMessage.Add("channel", "control");
            unsubscribeMessage.Add("event", "unsubscribe");
            unsubscribeMessage.Add("data", messageData);
            bbtSocket.Emit("message", unsubscribeMessage);
        }
        public void Write(string channel, string resource, bool isPrivate, JToken data)
        {
            var writeMessage = new JObject();
            var messageData = new JObject();
            messageData.Add("channel", isPrivate ? String.Format("private-{0}", channel) : channel);
            messageData.Add("resource", resource);
            messageData.Add("data", data);
            writeMessage.Add("version", APIVersion);
            writeMessage.Add("channel", "stream");
            writeMessage.Add("event", "write");
            writeMessage.Add("data", messageData);
            bbtSocket.Emit("message", writeMessage);
        }
        public void Publish(string channel, string resource, bool isPrivate, JToken data)
        {
            var writeMessage = new JObject();
            var messageData = new JObject();
            messageData.Add("channel", isPrivate ? String.Format("private-{0}", channel) : channel);
            messageData.Add("resource", resource);
            messageData.Add("data", data);
            writeMessage.Add("version", APIVersion);
            writeMessage.Add("channel", "stream");
            writeMessage.Add("event", "emit");
            writeMessage.Add("data", messageData);
            bbtSocket.Emit("message", writeMessage);
        }
        public void Disconnect()
        {
            bbtSocket.Disconnect();
            Subscriptions = null;
            Connected = false;
        }

        #endregion

        #region Private Methods

        private void SendSubscription(Subscription subscription)
        {
            var subscriptionMessage = new JObject();
            var subscriptionData = new JObject();
            subscriptionData.Add("channel", subscription.ChannelInternalName);
            subscriptionData.Add("resource", subscription.Resource);
            subscriptionData.Add("ttl", 0);
            subscriptionData.Add("read", subscription.Read);
            subscriptionData.Add("write", subscription.Write);
            subscriptionMessage.Add("version", APIVersion);
            subscriptionMessage.Add("channel", "control");
            subscriptionMessage.Add("event", "subscribe");
            subscriptionData.Add("sid", Sid);
            if (subscription.Private || subscription.Write)
            {
                subscriptionData.Add("sig", SignSubscription(subscription));
            }
            subscriptionMessage.Add("data", subscriptionData);
            bbtSocket.Emit("message", subscriptionMessage);
        }
        private string SignSubscription(Subscription subscription)
        {
            var toSign = String.IsNullOrEmpty(subscription.UserId) ? String.Format("{0}:{1}.{2}:ttl={3}:read={4}:write={5}",
                Sid, subscription.ChannelInternalName, subscription.Resource, "0", subscription.Read.ToString().ToLower(),
                subscription.Write.ToString().ToLower()) :
                String.Format("{0}:{1}.{2}:ttl={3}:read={4}:write={5}:userid={6}",
                Sid, subscription.ChannelInternalName, subscription.Resource, "0", subscription.Read.ToString().ToLower(),
                subscription.Write.ToString().ToLower(), subscription.UserId);
            return String.Format("{0}:{1}", AccessKey, Utilities.GenerateHMACHash(toSign, SecureKey));
        }

        #endregion       

        #region Events Hanlders

        public event EventHandler SocketConnected;
        public event EventHandler<EventArgs<Message>> MessageReceived;
        public event EventHandler<EventArgs<string>> ConnectionFailed;
        public event EventHandler<EventArgs<string>> ErrorOccured;

        protected virtual void OnSocketConnected(EventArgs e)
        {
            EventHandler handler = SocketConnected;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        protected virtual void OnMessageReceived(EventArgs<Message> e)
        {
            EventHandler<EventArgs<Message>> handler = MessageReceived;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        protected virtual void OnConnectionFailed(EventArgs<string> e)
        {
            EventHandler<EventArgs<string>> handler = ConnectionFailed;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        protected virtual void OnError(EventArgs<string> e)
        {
            EventHandler<EventArgs<string>> handler = ErrorOccured;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        #endregion
    }
}
