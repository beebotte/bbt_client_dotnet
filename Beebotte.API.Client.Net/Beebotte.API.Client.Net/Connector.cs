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
        private List<Subscription> subscriptions;

        #endregion

        #region Properties

        public string ConnectionStatus{ get; set; }

        public List<Subscription> Subscriptions {
            get {
                return subscriptions;
            }
        }

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
            ConnectionStatus = Constants.Disconnected;
            var options = new IO.Options();
            options.Port = Port;
            options.QueryString = String.Format("key={0}", AccessKey);
            bbtSocket = IO.Socket(Uri, options);
            bbtSocket.On("message", (data) =>
            {
                var receivedMessage = JsonConvert.DeserializeObject<Message>(data.ToString());
                if (subscriptions != null)
                {
                    var subs = from s in subscriptions
                               where
                                   String.Equals(s.ChannelInternalName, receivedMessage.channel, StringComparison.CurrentCultureIgnoreCase) &&
                                   String.Equals(s.Resource, receivedMessage.resource, StringComparison.CurrentCultureIgnoreCase)
                               select s;
                    foreach (var subsription in subs)
                    {
                        subsription.MessageReceived(new EventArgs<Message>(receivedMessage));
                    }
                }
            });

            bbtSocket.On("getsid", (data) =>
            {
                Sid = data.ToString();
                if (String.Equals(ConnectionStatus, Constants.ReConnecting, StringComparison.InvariantCultureIgnoreCase))
                {
                    ReConnected(EventArgs.Empty);
                    foreach (var subscription in subscriptions)
                    {
                        SendSubscription(subscription);
                    }
                }
                else if (String.Equals(ConnectionStatus, Constants.Disconnected, StringComparison.InvariantCultureIgnoreCase))
                {
                    Connected(EventArgs.Empty);
                }
                ConnectionStatus = Constants.Connected;
            });

            bbtSocket.On(Socket.EVENT_RECONNECTING, () =>
            {
                ConnectionStatus = Constants.ReConnecting;
                ReConnecting(EventArgs.Empty);
            });

            bbtSocket.On(Socket.EVENT_CONNECT_TIMEOUT, (u) =>
            {
                TimeOut(EventArgs.Empty);
            });

            bbtSocket.On(Socket.EVENT_RECONNECT_FAILED, (u) =>
            {
                ReConnectionFailed(EventArgs.Empty);
            });

            bbtSocket.On(Socket.EVENT_RECONNECT_ERROR, (u) =>
            {
                ReConnectionError(new EventArgs<string>(String.Format("Unable to reconnect. Error Message:{0}", u)));
            });

            bbtSocket.On(Socket.EVENT_CONNECT_ERROR, (u) =>
            {
                ConnectionError(new EventArgs<string>(String.Format("Unable to connect. Error Message:{0}", u)));
            });

            bbtSocket.On(Socket.EVENT_ERROR, (u) =>
            {
                ConnectionError(new EventArgs<string>(String.Format("An error has occured. {0}", u)));
            });

            bbtSocket.On(Socket.EVENT_DISCONNECT, (u) =>
            {
                Disconnected(EventArgs.Empty);
                ConnectionStatus = Constants.Disconnected;
            });
        }
        public Subscription Subscribe(string channel, string resource, bool isPrivate, bool read, bool write)
        {
            if (subscriptions == null)
                subscriptions = new List<Subscription>();
            var subscription = new Subscription(channel, resource, isPrivate, read, write);
            subscriptions.Add(subscription);
            SendSubscription(subscription);
            return subscription;
        }
        public void Unsubscribe(string channel, string resource, bool isPrivate)
        {
            var removedSubsciptions = from subs in subscriptions where subs.Channel.Equals(channel) && subs.Resource.Equals(resource) select subs;
            if (removedSubsciptions != null && subscriptions.Count > 0)
            {
                foreach (var subs in removedSubsciptions)
                {
                    subscriptions.Remove(subs);
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
            if (bbtSocket != null)
                bbtSocket.Disconnect();
            subscriptions = null;
            ConnectionStatus = Constants.Disconnected;
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

        public event EventHandler OnConnected;
        public event EventHandler OnReconnecting;
        public event EventHandler OnReconnected;
        public event EventHandler OnTimeout;
        public event EventHandler<EventArgs<string>> OnConnectionError;
        public event EventHandler<EventArgs<string>> OnReConnectionError;
        public event EventHandler<EventArgs<string>> OnError;
        public event EventHandler OnDisconnected;
        public event EventHandler OnReconnectionFailed;

        protected virtual void Connected(EventArgs e)
        {
            EventHandler handler = OnConnected;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void ReConnecting(EventArgs e)
        {
            EventHandler handler = OnReconnecting;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void TimeOut(EventArgs e)
        {
            EventHandler handler = OnTimeout;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void ReConnected(EventArgs e)
        {
            EventHandler handler = OnReconnected;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        protected virtual void Disconnected(EventArgs e)
        {
            EventHandler handler = OnDisconnected;
            if (handler != null)
            {
                handler(this, e);
            }
        }
       
        protected virtual void ConnectionError(EventArgs<string> e)
        {
            EventHandler<EventArgs<string>> handler = OnConnectionError;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void ReConnectionError(EventArgs<string> e)
        {
            EventHandler<EventArgs<string>> handler = OnReConnectionError;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void ReConnectionFailed(EventArgs e)
        {
            EventHandler handler = OnReconnectionFailed;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected virtual void ErrorOccured(EventArgs<string> e)
        {
            EventHandler<EventArgs<string>> handler = OnError;
            if (handler != null)
            {
                handler(this, e);
            }
        }
        #endregion
    }
}
