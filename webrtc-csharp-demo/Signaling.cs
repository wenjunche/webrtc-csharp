using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

using SocketIOClient;
using SocketIOClient.WebSocketClient;

namespace webrtc_csharp_demo
{
    class Signaling
    {
        private Configuration configuration;
        private SocketIO socketIO;
        private string socketId;
        private CookieContainer cookieContainer;
        public bool PeerLeader
        { get; set; }
        public EventHandler<string> OnRtcConfig
        { get; set; }
        public EventHandler<string> OnTrickleReady
        { get; set; }

        public EventHandler<JsonElement> OnSdpMessage
        { get; set; }

        public Signaling(Configuration configuration)
        {
            this.configuration = configuration;
            this.PeerLeader = false;
        }

        public Task Initialize()
        {
            this.cookieContainer = new CookieContainer();
            this.MakeHTTPRequest($"{configuration.SignalingBaseUrl}/api/auth/check");

            var optios = new SocketIOOptions { };
            this.socketIO = new SocketIO(configuration.SignalingBaseUrl);
            var socket = this.socketIO.Socket as ClientWebSocket;
            socket.Config = options =>
            {
                options.Cookies = this.cookieContainer;
            };

            this.socketIO.OnConnected += Client_OnConnected;
            this.socketIO.OnError += Client_OnError;
            this.socketIO.OnDisconnected += Client_OnDisconnected;

            this.socketIO.On("ready", resp =>
            {
                var leader = resp.GetValue<String>(1);
                Console.WriteLine($"SocketIO Ready leader {leader}");
                this.PeerLeader = leader == this.socketId;
                var rtcConfig = this.MakeHTTPRequest($"{this.configuration.SignalingBaseUrl}/api/webrtc/rtcConfig");
                this.OnRtcConfig.Invoke(this, rtcConfig);
            });

            this.socketIO.On("joined", resp =>
            {
                var room = resp.GetValue<String>(0);
                var clientId = resp.GetValue<String>(1);
                Console.WriteLine($"This peer has joined room {room} with client Id {clientId}");
            });

            this.socketIO.On("trickle", resp =>
            {
                var pairCode = resp.GetValue<String>(0);
                Console.WriteLine($"peer trickle ready {pairCode}");
                this.OnTrickleReady.Invoke(this, pairCode);
            });

            this.socketIO.On("message", resp =>
            {
                var m = resp.GetValue(0);
                Console.WriteLine($"signaling message {m}");
                this.OnSdpMessage.Invoke(this, m);
            });

            return this.socketIO.ConnectAsync();
        }

        private void Client_OnError(object sender, string e)
        {
            Console.WriteLine($"SocketIO Error {e}");
        }

        private void Client_OnDisconnected(object sender, string e)
        {
            Console.WriteLine("SocketIO Disonnected");
        }

        private async void Client_OnConnected(object sender, EventArgs e)
        {
            using (JsonDocument document = JsonDocument.Parse(this.socketIO.Id))
            {
                this.socketId = document.RootElement.GetProperty("sid").GetString();
            }
            Console.WriteLine($"SocketIO Connected {this.socketId}");

            await this.socketIO.EmitAsync("join", this.configuration.PairingCode);
        }

        public Task EmitAsync(string eventName, params object[] data)
        {
            return this.socketIO.EmitAsync(eventName, data);
        }

        private string MakeHTTPRequest(string url)
        {
            Console.WriteLine($"makeHTTPRequest {url}");
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.CookieContainer = this.cookieContainer;
            WebResponse response = request.GetResponse();
            Console.WriteLine(((HttpWebResponse)response).StatusDescription);
            using (Stream dataStream = response.GetResponseStream())
            {
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadToEnd();
                Console.WriteLine($"{responseFromServer}");
                return responseFromServer;
            }
        }


    }
}
