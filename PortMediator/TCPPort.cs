using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace PortMediator
{
    

    class TCPPort:Port
    {
        
        private bool acceptingEnabled = true;
        private int port_ = 11000;
        Socket listener = null;
        IPEndPoint localEndPoint = null;
        static byte[] address = { 192, 168, 91, 1 };

        Socket sender = null;
        static IPAddress remoteIP = new IPAddress(address);
        IPEndPoint remoteEndPoint = new IPEndPoint(remoteIP, 53557);
        public class EndPointHandler
        {
            public Socket workSocket = null;
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
            public StringBuilder sb = new StringBuilder();

            public string endPointName = null;
        }

        List<EndPointHandler> endpoints = new List<EndPointHandler>();

        ManualResetEvent endPointAccepted = new ManualResetEvent(false);
        public TCPPort(int port)
        {
            port_ = port;
        }

        private void WaitForNextData()
        {
            
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);
                do
                {
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);
                    endPointAccepted.WaitOne();
                    endPointAccepted.Reset();
                } while (acceptingEnabled);

            }
            catch (AggregateException e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public async override Task<bool> OpenPort()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
           // bool success = false;
            IPAddress ipAddress = null;
            foreach (var ipa in ipHostInfo.AddressList)
            {
                if(ipa.AddressFamily == AddressFamily.InterNetwork)
                {
                    ipAddress = ipa;
                }
            }
            if(ipAddress != null)
            {
                localEndPoint = new IPEndPoint(ipAddress, port_);
                listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                sender = new Socket(remoteIP.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            }
            Console.WriteLine("Host EP: " + localEndPoint.ToString());

            /*Action acceptEndpoints = delegate
            {
                try
                {
                    server.Bind(localEndPoint);
                    server.Listen(100);

                    do
                    {
                        server.BeginAccept(new AsyncCallback(AcceptCallback), server);
                        endPointAccepted.WaitOne();
                        endPointAccepted.Reset();
                    } while (acceptingEnabled);

                    server.BeginAccept(new AsyncCallback(AcceptCallback), server);
                    endPointAccepted.WaitOne();
                }
                catch (AggregateException e)
                {
                    Console.WriteLine(e.ToString());
                }
            };
            await Task.Run(acceptEndpoints);*/
                
            
            return true;

        }

        private void AcceptCallback(IAsyncResult asyncResult)
        {
            Socket server = (Socket) asyncResult.AsyncState;
            Socket handler = server.EndAccept(asyncResult);

            EndPointHandler newEndPointHandler = new EndPointHandler();
            newEndPointHandler.workSocket = handler;
            handler.BeginReceive(newEndPointHandler.buffer, 0, EndPointHandler.BufferSize, 0, new AsyncCallback(ReadCallback), newEndPointHandler);
        }

        private void ReadCallback(IAsyncResult asyncResult)
        {
            EndPointHandler eph = (EndPointHandler)asyncResult.AsyncState;
            Socket socket = eph.workSocket;

            int bytesRead = socket.EndReceive(asyncResult);
            if(bytesRead > 0)
            {
                byte[] data = new byte[bytesRead];
                Array.Copy(eph.buffer, 0, data, 0, bytesRead);
                OnDataReceived(data);
                socket.BeginReceive(eph.buffer, 0, EndPointHandler.BufferSize, 0, new AsyncCallback(ReadCallback), eph);
            }
            endPointAccepted.Set();

            //eph.workSocket.BeginReceive(eph.buffer, 0, EndPointHandler.BufferSize, 0, new AsyncCallback(ReadCallback), eph);
        }

        public async override Task<bool> StartReading()
        {
            bool success = false;
            Task.Run((Action)WaitForNextData);
            success = true;

            /*foreach(EndPointHandler eph in endpoints)
            {
                eph.workSocket.BeginReceive(eph.buffer, 0, EndPointHandler.BufferSize, 0, new AsyncCallback(identifyCallback), eph);
                endPointIdentified.WaitOne();
                if(eph.endPointName == null)
                {
                    success = false;
                }
                endPointIdentified.Reset();
            }
           bool success = true;
           try
           {
               foreach (EndPointHandler eph in endpoints)
               {
                   eph.workSocket.BeginReceive(eph.buffer, 0, EndPointHandler.BufferSize, 0, new AsyncCallback(ReadCallback), eph);
                   //endPointIdentified.WaitOne();
                   //endPointIdentified.Reset();
               }
           }
           catch(AggregateException e)
           {
               Console.WriteLine("e.Message");
               success = false;
           }*/
            return success;
        }
        ManualResetEvent step = new ManualResetEvent(true);
        public override void SendData(byte[] data)
        {
            step.WaitOne();
            step.Reset();
            sender.BeginConnect(remoteEndPoint, ConnectCallback, sender);
            step.WaitOne();
            step.Reset();
            sender.BeginSendTo(data, 0, data.Length, SocketFlags.None, remoteEndPoint, SendCallback, sender);
            step.WaitOne();
            step.Reset();
            sender.BeginDisconnect(true, DisconnectCallback, sender);

        }

        private void ConnectCallback(IAsyncResult asyncResult)
        {
            try
            {
                listener.EndConnect(asyncResult);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            step.Set();
        }

        private void DisconnectCallback(IAsyncResult asyncResult)
        {
            try
            {
                listener.EndDisconnect(asyncResult);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            step.Set();

        }

        private void SendCallback(IAsyncResult asyncResult)
        {
            try
            {
                int bytesSent = listener.EndSend(asyncResult);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            step.Set();
        }

        public override void ClosePort()
        {
            foreach(var eph in endpoints)
            {
                eph.workSocket.Shutdown(SocketShutdown.Both);
                eph.workSocket.Close();
            }
            endpoints.Clear();
        }
    }


}
