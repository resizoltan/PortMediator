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
        private TcpClient tcpClient = null;
        const int bufferSize = 1024;
        public event EventHandler<EventArgs> closed;

        public TCPPort(TcpClient tcpClient, Action<Client> NewClientHandler) : base(NewClientHandler)
        {
            this.tcpClient = tcpClient;
            this.tcpClient.ReceiveBufferSize = bufferSize;
            this.tcpClient.SendBufferSize = bufferSize;
            //StartWaitingForConnectionRequest();
        }

        public override string ID
        {
            get
            {
                return "TCP remote endpoint " + tcpClient.Client.RemoteEndPoint.ToString();
            }
        }

        public override void Open()
        {
            try
            {
                StartWaitingForConnectionRequest();
            }
            catch (Exception e)
            {
                e.Source = "PortMediator.TCPPort.Open() of " + ID + " -> " + e.Source;
                throw e;
            }
        }

        public override void Close()
        {
            try
            {
                OnClosed();
                tcpClient.Client.Shutdown(SocketShutdown.Both);
                tcpClient.Client.Close();
            }
            catch (Exception e)
            {
                e.Source = "TCPPeripheral.Close() of " + ID + " -> " + e.Source;
                throw e;
            }
        }

        private void OnClosed()
        {
            EventHandler<EventArgs> handler = closed;
            if(handler != null)
            {
                handler(this, new EventArgs());
            }
        }

        public override void StartReading()
        {
            if (!tcpClient.Connected)
            {
                Exception e = new Exception("TcpClient not connected");
                e.Source = "StartReading()";
                throw e;
            }

            readTask = Read();
        }

        public async Task Read()
        {
            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            while (tcpClient.Connected && !waitForConnectionRequestTaskCTS.IsCancellationRequested)
            {
                NetworkStream inputStream = tcpClient.GetStream();
                int dataLength = 0;

                dataLength = await inputStream.ReadAsync(buffer, 0, 100);

                if (dataLength == 0)
                {
                    Close();
                    break;
                }
                byte[] data = new byte[dataLength];
                Array.Copy(buffer, data, dataLength);
                OnDataReceived(data);
            }
        }

        public override void StartWaitingForConnectionRequest()
        {
            if (!tcpClient.Connected)
            {
                Exception e = new Exception("TcpClient not connected");
                e.Source = "StartWaitingForConnectionRequest()";
                throw e;
            }

            waitForClientConnectionTask = MonitorPort();

        }

        private async Task MonitorPort()
        {
            byte[] buffer = new byte[connectionRequestMessageLength];
            byte[] data = new byte[connectionRequestMessageLength];
            int bytesRead = 0;
            while (bytesRead != connectionRequestMessageLength && 
                tcpClient.Connected && 
                !waitForConnectionRequestTaskCTS.IsCancellationRequested)
            {
                NetworkStream inputStream = tcpClient.GetStream();
                int dataLength = 0;
                dataLength = await inputStream.ReadAsync(buffer, 0, connectionRequestMessageLength);
                if (dataLength == 0)
                {
                    Close();
                    break;
                }
                else if(dataLength <= connectionRequestMessageLength - bytesRead)
                {
                    Array.Copy(buffer, 0, data, bytesRead, dataLength);
                    bytesRead += dataLength;
                }
                else
                {
                    Array.Copy(buffer, 0, data, bytesRead, connectionRequestMessageLength - bytesRead);
                    bytesRead = connectionRequestMessageLength;
                }
            }
            OnConnectionRequest(this, data);
        }

        public override void Write(byte[] data)
        {

            if (tcpClient.Connected)
            {
                NetworkStream outputStream = tcpClient.GetStream();
                writeTask = outputStream.WriteAsync(data, 0, data.Length);
                outputStream.Flush();
            }
        }

        public override void StopReading(Client client)
        {
            if ((WaitForConnectionRequestTask.Status == TaskStatus.Running) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForActivation) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForChildrenToComplete) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingToRun))
            {
                readTaskCTS.Cancel();
            }
        }
    }

    class TCPPeripheral : Peripheral
    {
        readonly int localPortNumber = 11000;
        readonly byte[] wantedLocalIPAdressBytes = { 192, 168, 137, 1 };
        readonly IPAddress wantedLocalIPAddress = new IPAddress(new byte[]{ 192, 168, 137, 1 });
        public static IPEndPoint localEndPoint = null;
        TcpListener tcpListener = null;
        CancellationTokenSource acceptTcpClientTaskCTS = new CancellationTokenSource();

        public TCPPeripheral(Action<Client> NewClientHandler):base(NewClientHandler)
        {
            try
            {
                //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
                //IPAddress foundLocalIPAddress = ipHostInfo.AddressList.Single(
                //    ipAddress => ipAddress.Address == wantedLocalIPAdressBytes);
                
                localEndPoint = new IPEndPoint(wantedLocalIPAddress, localPortNumber);

                tcpListener = new TcpListener(localEndPoint);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error occured in TCPPeripheral()");
                Console.WriteLine("\tError source: " + e.Source);
                Console.WriteLine("\tError message: " + e.Message);
            }
        }

        public override void Start()
        {
            try
            {
                tcpListener.Start();
                CancellationToken acceptTcpClientTaskCT = acceptTcpClientTaskCTS.Token;
                listenForPortConnectionsTask = WaitForPortConnections();
            }
            catch(AggregateException e)
            {
                Console.WriteLine("Error occured in TCPPeripheral.Start() of " + localEndPoint.ToString());
                foreach (Exception innerException in e.InnerExceptions)
                {
                    Console.WriteLine("\tError source: " + innerException.Source);
                    Console.WriteLine("\tError message: " + innerException.Message);
                }
            }
            
        }

        private async Task WaitForPortConnections()
        {
            while (!acceptTcpClientTaskCTS.IsCancellationRequested)
            {

                TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                TCPPort tcpPort = new TCPPort(tcpClient, NewClientHandler);
                tcpPort.closed += PortClosed;
                ports.Add(tcpPort);
                tcpPort.Open();
            }
        }

        private void PortClosed(object sender, EventArgs eventArgs)
        {
            Port port = (Port)sender;
            ports.Remove((Port)port);
            Console.WriteLine("Port " + port.ID + " closed");
        }

        public override void Stop()
        {
            base.Stop();
            acceptTcpClientTaskCTS.Cancel();
            tcpListener.Stop();
            //try
            //{
            listenForPortConnectionsTask.Wait();
            //}
            //catch (AggregateException e)
            //{
            //    Console.WriteLine("Error occured in TCPPeripheral.Stop()");
            //    foreach (Exception innerException in e.InnerExceptions)
            //    {
            //        Console.WriteLine("\tError source: " + innerException.Source);
            //        Console.WriteLine("\tError message: " + innerException.Message);
            //    }
            //}
        }

        //public override void Close()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
