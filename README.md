Beebotte Client .Net SDK
========================

| what          | where                                  |
|---------------|----------------------------------------|
| overview      | http://beebotte.com/overview           |
| tutorials     | http://beebotte.com/tutorials          |
| apidoc        | http://beebotte.com/docs/restapi       |
| source        | https://github.com/beebotte/bbt_dotnet |

### Bugs / Feature Requests

Think you.ve found a bug? Want to see a new feature in beebotte? Please open an
issue in github. Please provide as much information as possible about the issue type and how to reproduce it.

    https://github.com/beebotte/bbt_client_dotnet/issues
    
## Install

Nuget Install: https://www.nuget.org/packages/Beebotte.API.Client.Net

    Install-package Beebotte.API.Client.Net
    
Cloning:
Clone the source code from github

    git clone https://github.com/beebotte/bbt_client_dotnet.git
    


  
## Usage
To use the library, you need to be a registered user. If this is not the case, create your account at <http://beebotte.com> and note your access credentials.

As a reminder, Beebotte resource description uses a two levels hierarchy:

* Channel: physical or virtual connected object (an application, an arduino, a coffee machine, etc) providing some resources
* Resource: most elementary part of Beebotte, this is the actual data source (e.g. temperature from a domotics sensor)
  
### Beebotte Constructor
Use your account API and secret keys to initialize Beebotte connector:

    string accesskey  = "YOUR_API_KEY";
    string secretkey  = "YOUR_SECRET_KEY";
    string uri   = "http://ws.sandbox.beebotte.com";
    var connector = new Connector(accesskey, secretkey, uri);
    
### Connecting and subscribing to a resource
After having initialized Beebotte connector, use the 'Connect' method To connect to Beebotte:

    connector.Connect();

In order to subscribe to a resource:

    string channelName  = "YOUR_Channel"; //the channel name to subscribe to
    string resourceName  = "YOUR_Resource"; //the resource to subscribe to
    bool isPrivateChannel = true; //Boolean indicating if the channel is private
    bool readAccess = true; //Boolean indicating if the connection has read access on the channel/resource
    bool writeAccess = true; //Boolean indicating if the connection has write access on the channel/resource
    
    connector.SocketConnected += (u, m) =>
      {
          connector.Subscribe(channelName, resourceName , isPrivateChannel, readAccess , writeAccess);
          connector.MessageReceived += (i, n) =>
          {
              MessageBox.Show(n.Message.data); //Add here the code you want to execute on message received.
          };
      };
    
### Writing Data
You can write data to the resource you're subscribed to using:

    connector.Write(channelName, resourceName, isPrivateChannel, dataToWrite);
   
### Publishing Data
You can publish data to the resource you're subscribed to using:

    connector.Write(channelName, resourceName, isPrivateChannel, dataToPublish);
