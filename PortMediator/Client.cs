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
            [TYPE.BOOTCOMMANDER] = "BootCommander",
            [TYPE.MATLAB] = "Matlab",
            [TYPE.CONSOLE] = "Console"
        };
        //static Dictionary<TYPE, byte> clientMap = new Dictionary<TYPE, Client> { { TYPE.MATLAB, MatlabClient } };
        const byte closeSignal = 0xfc;

        Port port;
        public TYPE type;
        public string name;
        Communication.Packet packetInReceiving = new Communication.Packet();
        //public bool isOpen;
        //public bool canSend;
        //public bool canReceive;

        public event EventHandler<PacketReceivedEventArgs> DataReceived;

        public Client(TYPE type, string name, Port port)
        {
            this.type = type;
            this.name = name;
            this.port = port;
            this.port.DataReceived += ProcessReceivedData;
            this.port.Closes += SendCloseSignal;
        }

        public void SendData(Communication.Packet packet)
        {
            try
            {
                port.SendData(packet.xcp);
            }
            catch(Exception e)
            {
                Console.WriteLine("Could not send data to client " + name + "on " + e.Source + ": " + e.Message);
            }
        }

        public void OnDataReceived(byte[] data)
        {
            if (packetInReceiving.IsEmpty())
            {
                packetInReceiving = Communication.Packet.CreateFromXCP(data);
            }
            else
            {
                packetInReceiving.Add(data);
            }

            if (packetInReceiving.IsFinished())
            {
                EventHandler<PacketReceivedEventArgs> handler = DataReceived;
                if (handler != null)
                {
                    PacketReceivedEventArgs args = new PacketReceivedEventArgs();
                    args.packet = packetInReceiving;
                    packetInReceiving.Clear();
                    handler(this, args);
                }
                else
                {
                    //might throw exception here
                }
            }        
        }

        //public class DataReceivedEventArgs : EventArgs
        //{
        //    public Client port { get; set; }
        //    public byte[] data { get; set; }
        //}

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

        public void SendCloseSignal(object sender, Port.CloseEventArgs eventArgs)
        {
            port.SendData(new byte[] { closeSignal });
        }

        public void StartReading()
        {
            port.StartReading();
        }

        public void ProcessReceivedData(object port, BytesReceivedEventArgs eventArgs)
        {
            OnDataReceived(eventArgs.data);
        }

        
    }

}
