using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;

using Microsoft.MixedReality.WebRTC;
using SocketIOClient;
using SocketIOClient.WebSocketClient;
using System.Text.Json;

namespace webrtc_csharp_demo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var p = new Program();
            var config = new Configuration 
            { 
                PairingCode = "DotnetLove" +
                "",
                SignalingBaseUrl = "https://webrtc-signaling-dev.openfin.co"
            };
            var conn = new IceConnection(config);
            Channel channel1 = null;

            conn.ChannelAdded += (object sender, Channel channel) =>
            {
                Console.WriteLine($"Got a new channel {channel.Name}");
                channel1 = channel;
                channel1.MessageReceived += (object sender2, string data) =>
                {
                    Console.WriteLine($"new message: {data}");
                };
                channel1.StateChanged += (object sender2, Channel.ChannelState state) =>
                {
                    Console.WriteLine($"new channel state: {state}");
                };
            };

            await conn.Initialize();

            string cmd;
            while ((cmd = Console.ReadLine()) != null)
            {
                if (cmd == "bye")
                {
                    conn.Close();
                }
                else
                {
                    channel1?.SendMessage(cmd);
                }
            }
        }

    }

}
