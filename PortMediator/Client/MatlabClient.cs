using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    class MatlabClient : Client
    {
        public MatlabClient(string name, Port port):base(TYPE.MATLAB, name, port)
        {

        }

        public override void SendData(Communication.Packet packet)
        {
            try
            {
                port.Write(packet.xcp);
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
                packetInReceiving = Communication.Packet.CreateNewFromXCP(eventArgs.data, true);
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
