using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    

    public class Client
    {
        public enum TYPE
        {
            UNIDENTIFIED,
            MOUSE,
            BOOTCOMMANDER,
            MATLAB,
            CONSOLE,
            TYPECOUNT
        }
        public static Dictionary<TYPE, string> typenames = new Dictionary<TYPE, string>
        {
            [TYPE.UNIDENTIFIED] = "unidentified",
            [TYPE.MOUSE] = "mouse",
            [TYPE.BOOTCOMMANDER] = "bootcommander",
            [TYPE.MATLAB] = "matlab",
            [TYPE.CONSOLE] = "console"
        };
        static readonly byte[] closeSignal = { 0xfc };

        protected Port port;
        public TYPE type { get; }
        public string name { get; set; }
        protected Communication.Packet packetInReceiving = new Communication.Packet();

        public event EventHandler<PacketReceivedEventArgs> DataReceived;

        protected Client(TYPE type, string name, Port port)
        {
            this.type = type;
            this.name = name;
            this.port = port;
            this.port.DataReceived += ProcessReceivedData;
        }

        public static Client CreateNew(TYPE type, string name, Port port)
        {
            Client client = null;
            switch (type)
            {
                case TYPE.MATLAB:
                    client = new MatlabClient(name, port);
                    break;
                case TYPE.CONSOLE:
                    client = new ConsoleClient(name, port);
                    break;
                default:
                    client = new Client(type, name, port);
                    break;
            }
            return client;
        }

        public static TYPE Identify(byte id)
        {
            TYPE t = TYPE.UNIDENTIFIED;
            if (id < (byte)TYPE.TYPECOUNT)
            {
                t = (TYPE)id;
            }
            else
            {
                Exception e = new Exception("Client type " + id + " doesn't exist");
                e.Source = "Client.Identify()";
                throw e;
            }
            return t;

        }

        public void StartReading()
        {
            port.StartReading();
        }

        public virtual void SendData(Communication.Packet packet)
        {
            try
            {
                port.SendData(packet.rawData);
            }
            catch(AggregateException e)
            {
                e.InnerException.Source = "Client.SendData() of client " + name + " -> " + e.Source;
                throw e.InnerException;
            }
        }

        public async void Close()
        {
            try
            {
                port.SendData(closeSignal);
                await port.SendTask;
                port.Close();
                await port.WaitForAllOperationsToComplete();
            }
            catch(AggregateException e)
            {
                throw e.InnerException;
            }

        }

        public virtual void ProcessReceivedData(object port, BytesReceivedEventArgs eventArgs)
        {
            packetInReceiving = Communication.Packet.CreateNewFromRaw(eventArgs.data, false);
            OnPacketReadyForTransfer(packetInReceiving);
            packetInReceiving.Clear();
        }

        public void OnPacketReadyForTransfer(Communication.Packet packet)
        {
            EventHandler<PacketReceivedEventArgs> handler = DataReceived;
            if (handler != null)
            {
                PacketReceivedEventArgs args = new PacketReceivedEventArgs();
                args.packet = packet;
                handler(this, args);
            }
            else
            {
                /*cannot catch this exception, function is on a callback stack*/
                //Exception e = new Exception("DataReceived event handler is null, packet discarded");
                //e.Source = "Client.OnPacketReadyForTransfer() of client " + name;
                //throw e;
            }
        }

    }

}
