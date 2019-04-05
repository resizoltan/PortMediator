using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace PortMediator
{
    public abstract class Port
    {
        protected const int connectionRequestMessageLength = 3;

        //protected Action<Client> NewClientHandler = null;
        
        protected Task readTask = null;
        protected Task sendTask = null;
        protected Task waitForClientConnectionTask = null;

        public Task ReadTask {
            get { return readTask; }
        }
        public Task SendTask {
            get { return sendTask; }
        } 
        public Task WaitForConnectionRequestTask {
            get { return waitForClientConnectionTask; }
        }

        protected CancellationTokenSource readTaskCTS = new CancellationTokenSource();
        protected CancellationTokenSource sendTaskCTS = new CancellationTokenSource();
        protected CancellationTokenSource waitForConnectionRequestTaskCTS = new CancellationTokenSource();

        public event EventHandler<BytesReceivedEventArgs> DataReceived;
        public event EventHandler<NewClientEventArgs> NewClientConnected;
        public event EventHandler<PortClosedEventArgs> PortClosed;

        public abstract void Open();
        public abstract void Close();
        public abstract void StartReading();
        public abstract void StartWaitingForConnectionRequest();
        public abstract void StopReading(Client client);
        public abstract void SendData(byte[] data);

        public abstract string ID { get; }

        public async Task WaitForAllOperationsToComplete()
        {
            try
            {
                await ReadTask;
                await SendTask;
                await WaitForConnectionRequestTask;
            }
            catch (NullReferenceException) { }

        }

        protected void OnDataReceived(BytesReceivedEventArgs eventArgs)
        {
            EventHandler<BytesReceivedEventArgs> handler = DataReceived;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnClose(PortClosedEventArgs eventArgs)
        {
            EventHandler<PortClosedEventArgs> handler = PortClosed;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnConnectionRequest(ConnectionRequestEventArgs eventArgs)
        {
            byte[] bytes = eventArgs.bytes;
            if(bytes != null && bytes.Length == connectionRequestMessageLength)
            {
                try
                {
                    Client.TYPE type = Client.Identify((byte)(bytes[0]));
                    string name = Client.typenames[type] + "_" + Encoding.ASCII.GetString(bytes, 1, 2);
                    Client newClient = Client.CreateNew(type, name, this);
                    NewClientEventArgs newClientEventArgs = new NewClientEventArgs(newClient);
                    OnNewClient(newClientEventArgs);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error occured in Port.ConnectionRequested()");
                    Console.WriteLine("Error source:  " + e.Source);
                    Console.WriteLine("Error message: " + e.Message);
                    this.StartWaitingForConnectionRequest();
                }
            }
            else
            {
                this.StartWaitingForConnectionRequest();
            }
        }
        protected void OnNewClient(NewClientEventArgs eventArgs)
        {
            EventHandler<NewClientEventArgs> handler = NewClientConnected;
            handler?.Invoke(this, eventArgs);
        }
    }

    public abstract class Peripheral
    {
        protected List<Port> ports = new List<Port>();
        protected Task listenForPortConnectionsTask = null;
        protected CancellationTokenSource listenForPortConnectionsTaskCTS = new CancellationTokenSource();

        public EventHandler<NewPortEventArgs> NewPortOpened;

        public abstract void Start();
        public abstract void Stop();

        public abstract string ID { get; }

        protected void PortClosedEventHandler(object sender, PortClosedEventArgs eventArgs)
        {
            ports.Remove(sender);
        }
    }

    public class ConnectionRequestEventArgs : EventArgs
    {
        public byte[] bytes { get; set; }
        public ConnectionRequestEventArgs(byte[] bytes)
        {
            this.bytes = bytes;
        }
    }

    public class NewClientEventArgs : EventArgs
    {
        public Client client { get; set; }
        public NewClientEventArgs(Client client)
        {
            this.client = client;
        }
    }

    public class PacketReceivedEventArgs : EventArgs
    {
        public Communication.Packet packet { get; set; }
        public PacketReceivedEventArgs(Communication.Packet packet)
        {
            this.packet = packet;
        }
    }

    public class BytesReceivedEventArgs : EventArgs
    {
        public byte[] bytes { get; set; }
        public BytesReceivedEventArgs(byte[] bytes)
        {
            this.bytes = bytes;
        }
    }

    public class PortClosedEventArgs : EventArgs
    {
        public string initiator { get; set; }
        public PortClosedEventArgs(string initiator)
        {
            this.initiator = initiator;
        }
    }

    public class NewPortEventArgs : EventArgs
    {
        public Port port { get; set; }
        public NewPortEventArgs(Port port)
        {
            this.port = port;
        }
    }
}
