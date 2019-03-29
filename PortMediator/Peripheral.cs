using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    abstract class Peripheral
    {
        bool isOpen;
        bool canSend;
        bool canReceive;
        public string id { get; }

        List<Port> availablePorts;

        public abstract Task<bool> StartPeripheral();
        public abstract Task<bool> StopPeripheral();

        public event EventHandler<PortReceivedEventArgs> PortReceived;

    }



    public class PortReceivedEventArgs : EventArgs
    {
        public Port port { get; set; }
    }
}
