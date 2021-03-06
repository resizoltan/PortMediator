﻿using System;
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

        public TCPPort(TcpClient tcpClient)
        {
            this.tcpClient = tcpClient;
            this.tcpClient.ReceiveBufferSize = bufferSize;
            this.tcpClient.SendBufferSize = bufferSize;
            this.id = "TCP remote endpoint " + tcpClient.Client.RemoteEndPoint.ToString(); 
        }

        public override void Open()
        {
            if (!tcpClient.Connected)
            {
                throw new PortClosedException();
            }
            StartWaitingForConnectionRequest();
        }

        public override void Close()
        {
            if (!tcpClient.Connected)
            {
                throw new PortClosedException();
            }

            tcpClient.Client.Shutdown(SocketShutdown.Both);
            tcpClient.Client.Close();


        }

        public override void StartReading()
        {
            if (!tcpClient.Connected)
            {
                throw new PortClosedException();
            }
            readTask = Read();
        }

        public async Task Read()
        {
            try
            {
                byte[] buffer = new byte[tcpClient.ReceiveBufferSize];
                while (true)
                {
                    if (!tcpClient.Connected)
                    {
                        throw new PortClosedException();
                    }
                    NetworkStream inputStream = tcpClient.GetStream();
                    int dataLength = await inputStream.ReadAsync(buffer, 0, 100);

                    if (dataLength == 0)
                    {
                        Close();
                        PortClosedEventArgs portClosedEventArgs = new PortClosedEventArgs("Remote tcp endpoint");
                        OnClose(portClosedEventArgs);
                        break;
                    }
                    byte[] data = new byte[dataLength];
                    Array.Copy(buffer, data, dataLength);
                    BytesReceivedEventArgs eventArgs = new BytesReceivedEventArgs(data);
                    OnDataReceived(eventArgs);
                }
            }
            catch(Exception e)
            {
                ExceptionOccuredEventArgs eventArgs = new ExceptionOccuredEventArgs(e);
                OnReadExceptionOccured(eventArgs);
            }
           
        }

        public override void StartWaitingForConnectionRequest()
        {
            waitForClientConnectionTask = MonitorPort();
        }

        private async Task MonitorPort()
        {
            byte[] buffer = new byte[connectionRequestMessageLength];
            byte[] data = new byte[connectionRequestMessageLength];
            int bytesRead = 0;
            while (bytesRead != connectionRequestMessageLength)
            {
                try
                {
                    if (!tcpClient.Connected)
                    {
                        throw new PortClosedException();
                    }
                    NetworkStream inputStream = tcpClient.GetStream();
                    int dataLength = 0;
                    dataLength = await inputStream.ReadAsync(buffer, 0, connectionRequestMessageLength);
                    if (dataLength == 0)
                    {
                        Close();
                        break;
                    }
                    else if (dataLength <= connectionRequestMessageLength - bytesRead)
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
                catch(Exception e)
                {
                    ExceptionOccuredEventArgs exceptionOccuredEventArgs = new ExceptionOccuredEventArgs(e);
                    OnReadExceptionOccured(exceptionOccuredEventArgs);
                }
               
            }
            ConnectionRequestedEventArgs eventArgs = new ConnectionRequestedEventArgs(data);
            OnConnectionRequest(eventArgs);
        }

        public override void Write(byte[] data)
        {
            try
            {
                if (!tcpClient.Connected)
                {
                    throw new PortClosedException();
                }
                NetworkStream outputStream = tcpClient.GetStream();
                writeTask = outputStream.WriteAsync(data, 0, data.Length);
                outputStream.Flush();
            }
            catch (Exception e)
            {
                ExceptionOccuredEventArgs eventArgs = new ExceptionOccuredEventArgs(e);
                OnWriteExceptionOccured(eventArgs);
            }
        }

        public override void StopReading(Client client)
        {
            if ((WaitForConnectionRequestTask.Status == TaskStatus.Running) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForActivation) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForChildrenToComplete) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingToRun))
            {
                Close();
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

        public override string ID {
            get
            {
                return "TCPPeripheral on " + localEndPoint.ToString();
            }
        }

        public TCPPeripheral()
        {

            //IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            //IPAddress foundLocalIPAddress = ipHostInfo.AddressList.Single(
            //    ipAddress => ipAddress.Address == wantedLocalIPAdressBytes);
                
            localEndPoint = new IPEndPoint(wantedLocalIPAddress, localPortNumber);

            tcpListener = new TcpListener(localEndPoint);

        }

        public override void Start()
        {
            tcpListener.Start();
            //CancellationToken acceptTcpClientTaskCT = acceptTcpClientTaskCTS.Token;
            listenForPortConnectionsTask = WaitForPortConnections();
        }

        private async Task WaitForPortConnections()
        {
            try
            {
                while (true)
                {
                    TcpClient tcpClient = await tcpListener.AcceptTcpClientAsync();
                    TCPPort tcpPort = new TCPPort(tcpClient);
                    PortRequestedEventArgs eventArgs = new PortRequestedEventArgs(tcpPort);
                    OnPortRequested(eventArgs);
                }
            }
            catch(Exception e)
            {
                ExceptionOccuredEventArgs eventArgs = new ExceptionOccuredEventArgs(e);
                OnWaitForPortConnectionsExceptionOccured(eventArgs);
            }
        }

        public override void Stop()
        {
            tcpListener.Stop();
        }


    }
}
