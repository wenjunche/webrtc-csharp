using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.MixedReality.WebRTC;

namespace webrtc_csharp_demo
{
    class Channel
    {
        //
        // Summary:
        //     Connection state of a channel.
        public enum ChannelState
        {
            //
            // Summary:
            //     The channel has just been created, and negotiating is underway to establish
            //     a link between the peers. The channel cannot be used to send/receive yet.
            Connecting = 0,
            //
            // Summary:
            //     The channel is open and ready to send and receive messages.
            Open = 1,
            //
            // Summary:
            //     The channel is being closed, and is not available anymore for data exchange.
            Closing = 2,
            //
            // Summary:
            //     The channel reached end of life and can be destroyed. It cannot be re-connected;
            //     instead a new data channel must be created.
            Closed = 3
        }

        internal DataChannel DataChannel
        {
            get;
        }

        public EventHandler<string> MessageReceived
        { get; set; }
        public EventHandler<ChannelState> StateChanged
        { get; set; }

        public Channel(DataChannel dataChannel)
        {
            this.DataChannel = dataChannel;

            this.DataChannel.MessageReceived += DataChannel_MessageReceived;
            this.DataChannel.StateChanged += DataChannel_StateChanged;
        }

        private void DataChannel_StateChanged()
        {
            Console.WriteLine($"channel {this.Name} state change {this.DataChannel.State}");
            this.StateChanged?.Invoke(this, (ChannelState) this.DataChannel.State);
        }

        private void DataChannel_MessageReceived(byte[] obj)
        {
            var payload = Encoding.UTF8.GetString(obj);
            this.MessageReceived?.Invoke(this, payload);
        }

        public string Name
        {
            get { return this.DataChannel.Label;  }
        }

        public void SendMessage(string data)
        {
            var bytes = Encoding.UTF8.GetBytes(data);
            this.DataChannel.SendMessage(bytes);
        }

        public void Close()
        {
            // looks like Close not supported in DataChannel class
        }
    }
}
