using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    abstract class Peripheral
    {
        protected bool isRunning;
        public string id { get; }


        public abstract Task<bool> StartPeripheral();
        public abstract Task<bool> StopPeripheral();
        public abstract void ClosePeripheral();
        public abstract void WaitForConnectionRequest(string portID);


        public abstract Task<bool> OpenPort(string portName, Client client);
        public abstract void ClosePort(Client client);
        public abstract Task<bool> StartReadingPort(Client client);
        public abstract void StopReadingPort(Client client);

        public abstract void SendData(Client client, byte[] data);

        public event EventHandler<PortReceivedEventArgs> PortReceived;


    }

    

    public class PortReceivedEventArgs : EventArgs
    {
        public Client port { get; set; }
    }
}
