using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;


namespace PortMediator
{
    class TCPPeripheral : Peripheral
    {
        const int localPortNumber = 11000;
        TcpListener tcpListener = null;



        public override Task<bool> Start()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> Stop()
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            throw new NotImplementedException();
        }
    }
}
