using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    public abstract class Port
    {

        public abstract Task<bool> Open(string portName, Client client);
        public abstract void Close();
        public abstract Task<bool> StartReading();
        public abstract void StopReading(Client client);
        public abstract string GetID();

        public abstract void SendData(byte[] data);

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        public void OnDataReceived(byte[] data)
        {
            EventHandler<DataReceivedEventArgs> handler = DataReceived;
            if (handler != null)
            {
                DataReceivedEventArgs args = new DataReceivedEventArgs();
                args.data = data;
                handler(this, args);
            }
        }

        public class DataReceivedEventArgs : EventArgs
        {
            public byte[] data { get; set; }
        }

        //public class SendFailedException : Exception
        //{
        //    public SendFailedException(Exception couse) : base("Could not send data on " + couse.Source + ": " + couse.Message)
        //    {

        //    }
        //}
    }

    abstract class Peripheral
    {
        
        protected bool isRunning;
        public string id { get; }


        public abstract Task<bool> StartPeripheral();
        public abstract Task<bool> StopPeripheral();
        public abstract void ClosePeripheral();
        public abstract void WaitForConnectionRequest(string portID);


        

        public event EventHandler<PortReceivedEventArgs> PortReceived;


    }

    

    public class PortReceivedEventArgs : EventArgs
    {
        public Client port { get; set; }
    }
}
