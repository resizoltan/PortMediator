using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    abstract public class Port
    {
        int type;
        bool isOpen;
        bool canSend;
        bool canReceive;

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        public abstract Task<bool> OpenPort();
        public abstract void ClosePort();
        public abstract Task<bool> StartReadingPort();
        public abstract void StopReadingPort();

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

        public class DataReceivedEventArgs : EventArgs
        {
            public Port port { get; set; }
            public byte[] data { get; set; }
        }

        public int Identify(byte[] fromData)
        {
            new NotImplementedException();
            return 0;
        }
    }

}
