using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    class MouseClient : Client
    {
        public MouseClient(string name, Port port) : base(TYPE.MOUSE, name, port)
        {

        }

        public override void SendData(Communication.Packet packet)
        {
            try
            {
                port.SendData(packet.xcp);
            }
            catch (AggregateException e)
            {
                e.Source = "MatlabClient.SendData() of client " + name + " -> " + e.Source;
                throw e;
            }
        }


        public override void ProcessReceivedData(object port, BytesReceivedEventArgs eventArgs)
        {
            if (packetInReceiving.IsEmpty())
            {
                packetInReceiving = Communication.Packet.CreateNewFromXCP(eventArgs.data, false);
            }
            else
            {
                packetInReceiving.Add(eventArgs.data);
            }

            if (packetInReceiving.IsFinished())
            {
                OnPacketReadyForTransfer(packetInReceiving);
                packetInReceiving.Clear();
            }
        }
    }
}
