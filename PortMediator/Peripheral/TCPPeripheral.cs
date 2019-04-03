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
    class TCPPort : Port
    {

        public TCPPort(TcpClient tcpClient)
        {

        }

        public override string GetID()
        {
            throw new NotImplementedException();
        }

        public override void Open(Peripheral serialPeripheral)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            throw new NotImplementedException();
        }

        public override void StartReading()
        {
            throw new NotImplementedException();
        }

        public override void StartWaitingForConnectionRequest()
        {
            throw new NotImplementedException();
        }

        public override Task SendData(byte[] data)
        {
            throw new NotImplementedException();
        }

        public override void StopReading(Client client)
        {
            throw new NotImplementedException();
        }
    }

    class TCPPeripheral : Peripheral
    {
        const int localPortNumber = 11000;
        IPEndPoint localEndPoint = null;
        TcpListener tcpListener = null;

        CancellationTokenSource acceptTcpClientTaskCTS = new CancellationTokenSource();

        public TCPPeripheral()
        {
            try
            {
                IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                IPAddress localIPAddress = ipHostInfo.AddressList.Single(
                    ipAddress => ipAddress.AddressFamily == AddressFamily.InterNetwork);
                localEndPoint = new IPEndPoint(localIPAddress, localPortNumber);

                tcpListener = new TcpListener(localEndPoint);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error occured in TCPPeripheral() of " + localEndPoint.ToString());
                Console.WriteLine("\tError source: " + e.Source);
                Console.WriteLine("\tError message: " + e.Message);
            }
        }

        public override void Start()
        {
            tcpListener.Start();
            CancellationToken acceptTcpClientTaskCT = acceptTcpClientTaskCTS.Token;
            Task.Factory.StartNew(delegate
            {
                while (!acceptTcpClientTaskCTS.IsCancellationRequested)
                {
                    TcpClient tcpClient = tcpListener.AcceptTcpClient();
                    TCPPort tcpPort = new TCPPort(tcpClient);
                    ports.Add(tcpPort);
                }
            }, acceptTcpClientTaskCT, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public override void Stop()
        {
            throw new NotImplementedException();
        }

        //public override void Close()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
