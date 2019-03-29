using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    

    public abstract class Client
    {
        public enum TYPE
        {
            UNIDENTIFIED,
            MOUSE,
            BOOTCOMMANDER,
            MATLAB,
            CONSOLE
        }
        //static Dictionary<TYPE, byte> clientMap = new Dictionary<TYPE, Client> { { TYPE.MATLAB, MatlabClient } };
        const byte closeSignal = 0xfc;

        Peripheral peripheral;
        public TYPE type;
        public string name;
        public bool isOpen;
        public bool canSend;
        public bool canReceive;

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        public void SendData(byte[] data)
        {
            peripheral.SendData(this, data);
        }

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
            public Client port { get; set; }
            public byte[] data { get; set; }
        }

        public TYPE Identify(byte[] fromData)
        {
            if (fromData.Length > 0)
            {
                type = (TYPE)fromData.First();
            }
            return type;
        }

        public void SendCloseSignal()
        {
            SendData(new byte[] { closeSignal });
        }

    }

}
