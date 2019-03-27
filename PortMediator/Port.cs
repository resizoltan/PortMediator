using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    abstract class Port
    {
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public abstract void SendData(byte[] data);
        protected void OnDataReceived(byte[] data)
        {
            EventHandler<DataReceivedEventArgs> handler = DataReceived;
            if (handler != null)
            {
                DataReceivedEventArgs args = new DataReceivedEventArgs();
                args.data = data;
                handler(this, args);
            }
        }
        public abstract Task<bool> OpenPort();
        public abstract void ClosePort();
        public abstract Task<bool> StartReading();
    }

    public class DataReceivedEventArgs : EventArgs
    {
        public byte[] data { get; set; }

    }

    class DataProcessor
    {

    }
}
