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

        public override string GetID()
        {
            return "TCP remote endpoint " + tcpClient.Client.RemoteEndPoint.ToString();
        }

        public override void Open()
        {
            try
            {
                StartWaitingForConnectionRequest();
            }
            catch (Exception e)
            {
                e.Source = "PortMediator.TCPPort.Open() of " + GetID() + " -> " + e.Source;
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
                e.Source = "TCPPeripheral.Close() of " + GetID() + " -> " + e.Source;
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
            try
            {
                readingTask = Task.Factory.StartNew(Read,
                    waitForConnectionRequestTaskCTS.Token,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch (AggregateException e)
            {
                e.Source = "WaitForConnectionRequest() -> " + e.Source;
                throw e;
            }
        }

        public void Read()
        {
            byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
            while (tcpClient.Connected && !waitForConnectionRequestTaskCTS.IsCancellationRequested)
            {
                NetworkStream inputStream = tcpClient.GetStream();
                int dataLength = 0;
                try
                {
                    dataLength = inputStream.Read(buffer, 0, connectionRequestMessageLength);
                }
                catch { }
                if (dataLength == 0)
                {
                    Close();
                    Exception e = new Exception("TcpClient not connected");
                    e.Source = "Read()";
                    throw e;
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
            try
            {
                readingTask = Task.Factory.StartNew(MonitorPort,
                    waitForConnectionRequestTaskCTS.Token,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch(AggregateException e)
            {
                e.Source = "WaitForConnectionRequest() -> " + e.Source;
                throw e;
            }
        }

        private void MonitorPort()
        {
            byte[] buffer = new byte[connectionRequestMessageLength];
            byte[] data = new byte[connectionRequestMessageLength];
            int bytesRead = 0;
            while (bytesRead != connectionRequestMessageLength)
            {
                if (!tcpClient.Connected)
                {
                    Exception e = new Exception("TcpClient not connected");
                    e.Source = "MonitorPort()";
                    throw e;
                }
                if (waitForConnectionRequestTaskCTS.IsCancellationRequested)
                {
                    Exception e = new Exception("Waiting for connection request canceled");
                    e.Source = "MonitorPort()";
                    throw e;
                }

                NetworkStream inputStream = tcpClient.GetStream();
                int dataLength = 0;
                try
                {
                    dataLength = inputStream.Read(buffer, 0, connectionRequestMessageLength);
                }
                catch { }
                if (dataLength == 0)
                {
                    Close();
                    Exception e = new Exception("No connection request received");
                    e.Source = "MonitorPort()";
                    throw e;
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
            ConnectionRequested(this, data);
        }

        public override Task SendData(byte[] data)
        {
            Task sendTask = Task.Run(delegate
            {
                if (tcpClient.Connected)
                {
                    NetworkStream outputStream = tcpClient.GetStream();
                    outputStream.Write(data, 0, data.Length);
                    outputStream.Flush();
                }
            });
            return sendTask;
        }

        public override void StopReading(Client client)
        {
            if ((readingTask.Status == TaskStatus.Running) ||
                (readingTask.Status == TaskStatus.WaitingForActivation) ||
                (readingTask.Status == TaskStatus.WaitingForChildrenToComplete) ||
                (readingTask.Status == TaskStatus.WaitingToRun))
            {
                readingTaskCTS.Cancel();
            }
        }
    }

    class TCPPeripheral : Peripheral
    {
        readonly int localPortNumber = 11000;
        readonly byte[] wantedLocalIPAdressBytes = { 192, 168, 137, 1 };
        readonly IPAddress wantedLocalIPAddress = new IPAddress(new byte[]{ 192, 168, 137, 1 });
        IPEndPoint localEndPoint = null;
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
                listeningForConnectionRequestsTask = Task.Factory.StartNew(delegate
                {
                    while (!acceptTcpClientTaskCTS.IsCancellationRequested)
                    {

                        TcpClient tcpClient = tcpListener.AcceptTcpClient();
                        TCPPort tcpPort = new TCPPort(tcpClient, NewClientHandler);
                        tcpPort.closed += PortClosed;
                        ports.Add(tcpPort);
                        tcpPort.Open();

                       
                    }
                }, acceptTcpClientTaskCT, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch(AggregateException e)
            {
                Console.WriteLine("Error occured in TCPPeripheral.Start() of " + localEndPoint.ToString());
                Console.WriteLine("\tError source: " + e.Source);
                Console.WriteLine("\tError message: " + e.Message);
            }
            
        }

        private void PortClosed(object sender, EventArgs eventArgs)
        {
            Port port = (Port)sender;
            ports.Remove((Port)port);
            Console.WriteLine("Port " + port.GetID() + " closed");
        }

        public async override void Stop()
        {
            //base.Stop();
            //acceptTcpClientTaskCTS.Cancel();
            //await listeningForConnectionRequestsTask;
            tcpListener.Stop();
        }

        //public override void Close()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
