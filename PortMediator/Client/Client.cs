﻿using System;
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

        public Port port { get; }
        public TYPE type { get; }
        public string name { get; set; }
        protected Communication.Packet packetInReceiving = new Communication.Packet();

        public event EventHandler<PacketReceivedEventArgs> DataReceived;

        protected Client(TYPE type, string name, Port port)
        {
            this.type = type;
            this.name = name;
            this.port = port;
            this.port.BytesReceived += ProcessReceivedData;
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
                case TYPE.MOUSE:
                    client = new MouseClient(name, port);
                    break;
                case TYPE.BOOTCOMMANDER:
                    client = new BootCommanderClient(name, port);
                    break;
                default:
                    client = new Client(TYPE.UNIDENTIFIED, name, port);
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

        public virtual void SendData(object receivedFromClient, PacketReceivedEventArgs eventArgs)
        {
            try
            {
                port.Write(eventArgs.packet.rawData);
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
                port.Write(closeSignal);
                await port.WriteTask;
                port.Close();
                await port.WaitForAllOperationsToComplete();
            }
            catch (NullReferenceException) { }

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
                PacketReceivedEventArgs args = new PacketReceivedEventArgs(packet);
                handler(this, args);
            }

        }

    }

}
