using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    class ConsoleClient :Client
    {
        public ConsoleClient(string name, Port port):base(TYPE.CONSOLE, name, port)
        {

        }

        public override void SendData(Communication.Packet packet)
        {
            try
            {
                port.SendData(packet.rawData);
            }
            catch (Exception e)
            {
                e.Source = "Client.SendData() of client " + name + " -> " + e.Source;
                throw e;
            }
        }

        public override void ProcessReceivedData(object port, BytesReceivedEventArgs eventArgs)
        {
            packetInReceiving = Communication.Packet.CreateNewFromRaw(eventArgs.data, false);
            OnPacketReadyForTransfer(packetInReceiving);
            packetInReceiving.Clear();
        }

    }
}
