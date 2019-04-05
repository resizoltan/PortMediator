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

        protected Action<Client> NewClientHandler = null;

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

        //protected bool readTaskCancelRequested = false;
        //protected bool sendTaskCancelRequested = false;
        //protected bool 
        //protected void CancelReadTask()

        protected CancellationTokenSource readTaskCTS = new CancellationTokenSource();
        protected CancellationTokenSource sendTaskCTS = new CancellationTokenSource();
        protected CancellationTokenSource waitForConnectionRequestTaskCTS = new CancellationTokenSource();

        public Port(Action<Client> NewClientHandler)
        {
            this.NewClientHandler = NewClientHandler;
        }
        public abstract void Open();
        public abstract void Close();
        public abstract void StartReading();
        public abstract void StartWaitingForConnectionRequest();
        public abstract void StopReading(Client client);
        public abstract string GetID();

        public abstract void SendData(byte[] data);

        public event EventHandler<BytesReceivedEventArgs> DataReceived;

        public void OnDataReceived(byte[] data)
        {
            EventHandler<BytesReceivedEventArgs> handler = DataReceived;
            if (handler != null)
            {
                BytesReceivedEventArgs args = new BytesReceivedEventArgs();
                args.data = data;
                handler(this, args);
            }
        }

        //public void OnClose(string reason)
        //{
        //    EventHandler<CloseEventArgs> handler = Closes;
        //    if (handler != null)
        //    {
        //        CloseEventArgs args = new CloseEventArgs();
        //        args.reason = reason;
        //        handler(this, args);
        //    }
        //}

        //public class CloseEventArgs : EventArgs
        //{
        //    public string reason { get; set; } = "unknown";
        //}

        public void ConnectionRequested(Port onPort, byte[] data)
        {
            if(data.Length == connectionRequestMessageLength)
            {
                try
                {
                    Client.TYPE type = Client.Identify((byte)(data[0]));
                    string name = Client.typenames[type] + "_" + Encoding.ASCII.GetString(data, 1, 2);
                    Client newClient = Client.CreateNew(type, name, this);
                    NewClientHandler(newClient);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error occured in Port.ConnectionRequested()");
                    Console.WriteLine("Error source:  " + e.Source);
                    Console.WriteLine("Error message: " + e.Message);
                    onPort.StartWaitingForConnectionRequest();
                }
            }
            else
            {
                onPort.StartWaitingForConnectionRequest();
            }
        }
    }

    public abstract class Peripheral
    {
        
        public string id { get; }
        protected List<Port> ports = new List<Port>();
        protected Action<Client> NewClientHandler = null;
        protected Task listenForPortConnectionsTask = null;

        public Peripheral(Action<Client> NewClientHandler)
        {
            this.NewClientHandler = NewClientHandler;
        }

        public abstract void Start();
        public virtual void Stop()
        {
            foreach (Port port in ports)
            {
                port.Close();
                try
                {
                    port.WaitForAllOperationsToComplete().Wait();
                }
                catch (NullReferenceException e)
                {

                }
            }
        }
        //public abstract void Close();        

        //public event EventHandler<NewClientEventArgs> NewClientReceived;
        //public void OnNewClient(Client client)
        //{
        //    EventHandler<NewClientEventArgs> handler = NewClientReceived;
        //    if (handler != null)
        //    {
        //        NewClientEventArgs args = new NewClientEventArgs();
        //        args.client = client;
        //        handler(this, args);
        //    }
        //}
    }

    public class NewClientEventArgs : EventArgs
    {
        public Client client { get; set; }
    }

    public class PacketReceivedEventArgs : EventArgs
    {
        public Communication.Packet packet { get; set; }
    }

    public class BytesReceivedEventArgs : EventArgs
    {
        public byte[] data { get; set; }
    }

}
