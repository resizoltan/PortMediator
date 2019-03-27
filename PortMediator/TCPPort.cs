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

        public class EndPointHandler
        {
            public Socket workSocket = null;
            public const int BufferSize = 1024;
            public byte[] buffer = new byte[BufferSize];
            public StringBuilder sb = new StringBuilder();

            public string endPointName = null;
        }

        List<EndPointHandler> endpoints = new List<EndPointHandler>();

        //private ManualResetEvent endPointIdentified = new ManualResetEvent(false);
        public TCPPort(int port)
        {
            port_ = port;
        }

        public override Task<bool> OpenPort()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, port_);

            Socket server = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                
            try
            {
                server.Bind(localEndPoint);
                server.Listen(100);

                ManualResetEvent endPointAccepted = new ManualResetEvent(false);
                do
                {
                    server.BeginAccept(new AsyncCallback(AcceptCallback), server);
                    endPointAccepted.WaitOne();
                    endPointAccepted.Reset();
                } while (acceptingEnabled);
    }
            catch(AggregateException e)
            {
                Console.WriteLine(e.ToString());
            }
            return Task<bool>.FromResult(true);

        }

        private void AcceptCallback(IAsyncResult asyncResult)
        {
            Socket server = (Socket) asyncResult.AsyncState;
            Socket handler = server.EndAccept(asyncResult);

            EndPointHandler newEndPointHandler = new EndPointHandler();
            newEndPointHandler.workSocket = handler;
            endpoints.Add(newEndPointHandler);
            //handler.BeginReceive(partnerHandler.buffer, 0, EndPointHandler.BufferSize, 0, new AsyncCallback(readCallback), partnerHandler);
        }

        private void ReadCallback(IAsyncResult asyncResult)
        {
            EndPointHandler eph = (EndPointHandler)asyncResult.AsyncState;
            Socket socket = eph.workSocket;

            int bytesRead = socket.EndReceive(asyncResult);
            byte[] data = new byte[bytesRead];
            Array.Copy(eph.buffer, 0, data, 0, bytesRead);
            OnDataReceived(data);
        }

        public async override Task<bool> StartReading()
        {
            /*bool success = true;
             foreach(EndPointHandler eph in endpoints)
             {
                 eph.workSocket.BeginReceive(eph.buffer, 0, EndPointHandler.BufferSize, 0, new AsyncCallback(identifyCallback), eph);
                 endPointIdentified.WaitOne();
                 if(eph.endPointName == null)
                 {
                     success = false;
                 }
                 endPointIdentified.Reset();
             }*/
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
            }
            return success;
        }

        public override void SendData(byte[] data)
        {
            throw new NotImplementedException();
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
