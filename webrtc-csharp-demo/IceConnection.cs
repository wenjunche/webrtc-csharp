using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

using Microsoft.MixedReality.WebRTC;
using System.IO;

namespace webrtc_csharp_demo
{
    class IceConnection
    {
        private Configuration configuration;
        private Signaling signaling;
        private bool peerTrickleReady;
        private string defaultChannelName;
        private PeerConnection peerConnection;
        private Channel defaultChannel;
        private Dictionary<string, Channel> channels;
        private bool peerConnectionInitialized;

        public string PairCode
        { get; set; }

        public EventHandler<Channel> OnChannelAdded
        { get; set; }


        public IceConnection(Configuration configuration)
        {
            this.configuration = configuration;
            this.peerTrickleReady = false;
            this.signaling = new Signaling(configuration);
            this.defaultChannelName = $"{this.configuration.PairingCode}:default";
            this.channels = new Dictionary<string, Channel>();
            this.peerConnectionInitialized = false;
        }

        public Task Initialize()
        {
            this.signaling.OnRtcConfig += Signaling_OnRtcConfig;
            this.signaling.OnTrickleReady += Signaling_OnTrickleReady;
            this.signaling.OnSdpMessage += Signaling_OnSdpMessage;
            return this.signaling.Initialize();
        }

        private async void Signaling_OnRtcConfig(object sender, string rtcConfig)
        {
            var iceServers = new List<IceServer>();
            // {"iceServers":[{"urls":["stun:webrtc-stund-dev.openfin.co:3478"],"username":"openfinny","credential":"7adjv&ut!-kL9n2K"}]}
            using (JsonDocument document = JsonDocument.Parse(rtcConfig))
            {
                foreach (var srv in document.RootElement.GetProperty("iceServers").EnumerateArray())
                {
                    var list = new List<string>();
                    foreach (var url in srv.GetProperty("urls").EnumerateArray())
                    {
                        list.Add(url.GetString());
                    }
                    var username = srv.GetProperty("username").GetString();
                    var credential = srv.GetProperty("credential").GetString();
                    iceServers.Add(new IceServer { Urls = list, TurnUserName = username, TurnPassword = credential });
                }
            }

            var config = new PeerConnectionConfiguration
            {
                IceServers = iceServers
            };

            this.peerConnection = new PeerConnection();
            this.peerConnection.Connected += PeerConnection_Connected;
            this.peerConnection.IceGatheringStateChanged += PeerConnection_IceGatheringStateChanged;
            this.peerConnection.IceStateChanged += PeerConnection_IceStateChanged;
            this.peerConnection.LocalSdpReadytoSend += PeerConnection_LocalSdpReadytoSend;
            this.peerConnection.IceCandidateReadytoSend += PeerConnection_IceCandidateReadytoSend;
            this.peerConnection.DataChannelAdded += PeerConnection_DataChannelAdded;
            this.peerConnection.DataChannelRemoved += PeerConnection_DataChannelRemoved;

            await this.peerConnection.InitializeAsync(config);
            this.peerConnectionInitialized = true;

            await this.signaling.EmitAsync("trickle", this.configuration.PairingCode);
            if (this.peerTrickleReady)
            {
                this.LeaderOffer();
            }
        }

        private void PeerConnection_DataChannelRemoved(DataChannel channel)
        {
            Console.WriteLine($"data channel removed ${channel.Label}");
            if (this.defaultChannelName != channel.Label)
            {
                var c = new Channel(channel);
                this.channels.Remove(c.Name);
            }
        }

        private void PeerConnection_DataChannelAdded(DataChannel channel)
        {
            Console.WriteLine($"data channel added ${channel.Label}");
            if (this.defaultChannelName != channel.Label)
            {
                var c = new Channel(channel);
                this.channels.Add(c.Name, c);
                this.OnChannelAdded?.Invoke(this, c);
            }
        }

        private async void PeerConnection_IceCandidateReadytoSend(IceCandidate candidate)
        {
            var ms = new MemoryStream();
            var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            writer.WriteString("type", "candidate");
            writer.WritePropertyName("candidate");
            writer.WriteStartObject();
            writer.WriteString("candidate", candidate.Content);
            writer.WriteString("sdpMid", candidate.SdpMid);
            writer.WriteNumber("sdpMLineIndex", candidate.SdpMlineIndex);
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();

            var payload = JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
            Console.WriteLine($"sending candidate {candidate.Content}");
            await this.signaling.EmitAsync("message", payload);
            writer.Dispose();
            ms.Dispose();
        }

        private async void PeerConnection_LocalSdpReadytoSend(SdpMessage message)
        {
            string typeStr = SdpMessage.TypeToString(message.Type);
            Console.WriteLine($"SdpMessage type: {typeStr}");
            var ms = new MemoryStream();
            var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();
            writer.WriteString("type", typeStr);
            writer.WriteString("sdp", message.Content);
            writer.WriteEndObject();
            writer.Flush();

            var answer = JsonDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
            Console.WriteLine($"sending answer {answer}");
            await this.signaling.EmitAsync("message", answer);
            writer.Dispose();
            ms.Dispose();
        }

        private void Signaling_OnTrickleReady(object sender, string pairCode)
        {
            this.peerTrickleReady = true;
            if (this.peerConnectionInitialized)
            {
                Console.WriteLine("lead offer after Signaling_OnTrickleReady");
                this.LeaderOffer();
            }
        }

        private async void Signaling_OnSdpMessage(object sender, JsonElement message)
        {
            string sdpType = message.GetProperty("type").GetString();
            var sdpMessage = new SdpMessage();
            switch (sdpType)
            {
                case "offer":
                    sdpMessage.Type = SdpMessageType.Offer;
                    sdpMessage.Content = message.GetProperty("sdp").GetString();
                    await this.peerConnection.SetRemoteDescriptionAsync(sdpMessage);
                    this.peerConnection.CreateAnswer();
                    break;
                case "answer":
                    sdpMessage.Type = SdpMessageType.Answer;
                    sdpMessage.Content = message.GetProperty("sdp").GetString();
                    await this.peerConnection.SetRemoteDescriptionAsync(sdpMessage);
                    break;
                case "candidate":
                    var candidate = new IceCandidate();
                    var newCandidate = message.GetProperty("candidate");
                    candidate.SdpMid = newCandidate.GetProperty("sdpMid").GetString();
                    candidate.SdpMlineIndex = newCandidate.GetProperty("sdpMLineIndex").GetInt32();
                    candidate.Content = newCandidate.GetProperty("candidate").GetString();
                    this.peerConnection.AddIceCandidate(candidate);
                    break;

            }
        }

        private void PeerConnection_IceStateChanged(IceConnectionState newState)
        {
            Console.WriteLine($"ICE state: {newState}");
        }

        private void PeerConnection_IceGatheringStateChanged(IceGatheringState newState)
        {
            Console.WriteLine($"ICE Gathering state: {newState}");
        }

        private void PeerConnection_Connected()
        {
            Console.WriteLine("PeerConnection: connected");
        }


        private void LeaderOffer()
        {
            if (this.signaling.PeerLeader)
            {
                this.InitializeOffer();
            }
        }

        private void InitializeOffer()
        {
            Console.WriteLine($"Initialize offer {this.configuration.PairingCode}");
            this.CreateDefaultChannel();
            this.CreateOffer();
        }

        private async void CreateDefaultChannel()
        {
            Console.WriteLine($"Creating default channel");
            var channel = await this.peerConnection.AddDataChannelAsync(this.defaultChannelName, true, true);
            this.defaultChannel = new Channel(channel);
        }

        private void CreateOffer()
        {
            Console.WriteLine($"Creating offer");
            this.peerConnection.CreateOffer();
        }

        public async Task<Channel> CreateChannel(string name)
        {
            var dataChannel = await this.peerConnection.AddDataChannelAsync(name, true, true);
            var channel = new Channel(dataChannel);
            this.channels.Add(channel.Name, channel);
            return channel;
        }
    }

}
