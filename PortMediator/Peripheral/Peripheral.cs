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

        protected Peripheral hostingPeripheral = null;

        protected Task readingTask = null;
        protected CancellationTokenSource readingTaskCancellationTokenSource = new CancellationTokenSource();
        protected CancellationTokenSource waitForConnectionRequestTaskCancellationTokenSource = new CancellationTokenSource();

        public abstract Task<bool> Open(Peripheral serialPeripheral);
        public abstract void Close();
        public abstract Task<bool> StartReading();
        public abstract Task<bool> WaitForConnectionRequest();
        public abstract void StopReading(Client client);
        public abstract string GetID();

        public abstract void SendData(byte[] data);

        public event EventHandler<BytesReceivedEventArgs> DataReceived;
        public event EventHandler<CloseEventArgs> Closes;

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


        public void OnClose(string reason)
        {
            EventHandler<CloseEventArgs> handler = Closes;
            if (handler != null)
            {
                CloseEventArgs args = new CloseEventArgs();
                args.reason = reason;
                handler(this, args);
            }
        }

        public class CloseEventArgs : EventArgs
        {
            public string reason { get; set; } = "unknown";
        }

        public void ConnectionRequested(byte[] data)
        {
            if(data.Length == 3)
            {
                try
                {
                    Client.TYPE type = Client.Identify((byte)(data[0] - Encoding.ASCII.GetBytes("0")[0]));
                    string name = Client.typenames[type] + "_" + Encoding.ASCII.GetString(data, 1, 2);
                    Client newClient = Client.CreateNew(type, name, this);
                    hostingPeripheral.OnNewClient(newClient);
                }
                catch(Exception e)
                {
                    Console.WriteLine("Error occured in Port.ConnectionRequested()");
                    Console.WriteLine("Error source:  " + e.Source);
                    Console.WriteLine("Error message: " + e.Message);
                }
                

            }
        }





        //public class SendFailedException : Exception
        //{
        //    public SendFailedException(Exception couse) : base("Could not send data on " + couse.Source + ": " + couse.Message)
        //    {

        //    }
        //}
    }

    public abstract class Peripheral
    {
        
        protected bool isRunning;
        public string id { get; }


        public abstract Task<bool> StartPeripheral();
        public abstract Task<bool> StopPeripheral();
        public abstract void ClosePeripheral();        

        public event EventHandler<NewClientEventArgs> NewClientReceived;
        public void OnNewClient(Client client)
        {
            EventHandler<NewClientEventArgs> handler = NewClientReceived;
            if (handler != null)
            {
                NewClientEventArgs args = new NewClientEventArgs();
                args.client = client;
                handler(this, args);
            }
        }
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
