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
    

    class TCPPort:Client
    {
        private int localPort = 11000;
        TcpListener server = null;
        ManualResetEvent readingStarted = new ManualResetEvent(false);
        ClientHandler activeBootloaderHost = null;
        static byte[] bootloaderStartCommand =  {2, 255, 0 };
        static byte[] bootloaderStopCommand = {1, 207};
        static public bool isBootloaderStartCommand(byte[] data)
        {
            bool answer = true;
            if (data.Length >= 3)
            {
                for(int b = 0; b < bootloaderStartCommand.Length; b++)
                {
                    if (data[b] != bootloaderStartCommand[b])
                    {
                        answer = false;
                        break;
                    }
                }
            }
            return answer;
        }
        static public bool isBootloaderStopCommand(byte[] data)
        {
            bool answer = true;
            if (data.Length >= 2)
            {
                for (int b = 0; b < bootloaderStopCommand.Length; b++)
                {
                    if (data[b] != bootloaderStopCommand[b])
                    {
                        answer = false;
                        break;
                    }
                }
            }
            return answer;
        }


        //static byte[] address = { 192, 168, 91, 1 };

        //static IPAddress remoteIP = new IPAddress(address);
        //IPEndPoint remoteEndPoint = new IPEndPoint(remoteIP, 53557);
        private class ClientHandler
        {
            public TcpClient clientSocket = null;
            public string clientName = null;

            public const int BufferSize = 1024;
            public byte[] inputBuffer = new byte[BufferSize];
            public byte[] outputBuffer = null;

            TCPPort basePort = null;

            public ClientHandler(TCPPort inBasePort)
            {
                basePort = inBasePort;
            }

            public void StartClient(TcpClient inClientSocket, string clientName)
            {
                this.clientSocket = inClientSocket;
                this.clientSocket.ReceiveBufferSize = BufferSize;
                this.clientSocket.SendBufferSize = BufferSize;
                this.clientName = clientName;
                Task.Factory.StartNew(Read, TaskCreationOptions.LongRunning);
            }

            public void CloseClient()
            {
                clientSocket.Client.Shutdown(SocketShutdown.Both);
                clientSocket.Client.Close();
            }

            /*public async string RequestIDFromClient()
            {
                byte[] requestIDCommand = { 1, 0xf1 }; //XCP: first byte is packet size, then command, then data
                SendData(requestIDCommand);

            }*/

            private void Read()
            {
                bool firstRead = true;
                while (clientSocket.Connected)
                {
                    //TODO: implement with NetworkStream.BeginRead
                    try
                    {
                        NetworkStream inputStream = clientSocket.GetStream();
                        int bytesRead = inputStream.Read(inputBuffer, 0, (int)clientSocket.ReceiveBufferSize);
                        if(bytesRead == 0)
                        {
                            CloseClient();
                            basePort.clients.Remove(this);
                            basePort.activeBootloaderHost = null;
                        }
                        else
                        {
                            int dataSize = 0;
                            byte[] dataOriginal = null;
                            byte[] dataBootloader = null;
                            byte[] data = null;
                            dataSize = bytesRead;
                            dataOriginal = new byte[dataSize];
                            Array.Copy(inputBuffer, 0, dataOriginal, 0, dataSize);
                            if (bytesRead >= 5)
                            {
                                dataSize = bytesRead - 4;
                                dataBootloader = new byte[dataSize + 1];
                                Array.Copy(inputBuffer, 4, dataBootloader, 1, dataSize);
                                dataBootloader[0] = (byte)(bytesRead - 4);
                                if (firstRead)
                                {
                                    if (isBootloaderStartCommand(dataBootloader))
                                    {
                                        basePort.activeBootloaderHost = this;
                                        data = dataBootloader;
                                    }
                                    firstRead = false;
                                }
                                else if (basePort.activeBootloaderHost != null && isBootloaderStopCommand(dataBootloader))
                                {
                                    CloseClient();
                                    basePort.clients.Remove(this);
                                    basePort.activeBootloaderHost = null;
                                    data = dataBootloader;
                                }
                                else if(basePort.activeBootloaderHost != null)
                                {
                                    data = dataBootloader;
                                }
                                else
                                {
                                    data = dataOriginal;
                                }
                            }
                            else
                            {
                                data = dataOriginal;
                            }

                            basePort.OnDataReceived(data);
                        }
                    }
                    catch(Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
            }

            public void SendData(byte[] data)
            {
                if (clientSocket.Connected)
                {
                    NetworkStream outputStream = clientSocket.GetStream();
                    outputStream.Write(data, 0, data.Length);
                    outputStream.Flush();
                }
            }
        }

        

        List<ClientHandler> clients = new List<ClientHandler>();

        public TCPPort(int port)
        {
            localPort = port;
        }

        public override Task<bool> OpenPort()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress localIPAdress = null;
            foreach (var ipa in ipHostInfo.AddressList)
            {
                if(ipa.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIPAdress = ipa;
                }
            }
            IPEndPoint localEndPoint = new IPEndPoint(localIPAdress, localPort);
            Console.WriteLine("Host EP: " + localEndPoint.ToString());

            server = new TcpListener(localIPAdress, localPort);

            server.Start();

            Task.Factory.StartNew(delegate
            {
                while (true)
                {
                    TcpClient client = server.AcceptTcpClient();
                    ClientHandler clientHandler = new ClientHandler(this);
                    clients.Add(clientHandler);
                    Task.Factory.StartNew(delegate
                    {
                        readingStarted.WaitOne();
                        clientHandler.StartClient(client, "dummyname");
                        Console.WriteLine("new client");
                    });

                }
            });

            return Task.FromResult(true) ;

        }

        public override Task<bool> StartReading()
        {
            readingStarted.Set();           
            return Task.FromResult(true);
        }

        public override void SendData(byte[] data)
        {
            byte[] TCPData = new byte[data.Length + 3];
            Array.Copy(data, 1, TCPData, 4, data.Length - 1);
            clients[0].SendData(TCPData);

            if (activeBootloaderHost != null)
            {
                activeBootloaderHost.SendData(TCPData);
                /*if (isBootloaderStopCommand(data))
                {
                    activeBootloaderHost = null;
                }*/
                /* foreach (var pb in TCPData)
                 {
                     Console.Write(pb + " ");
                 }
                 Console.WriteLine("");*/
            }
            else
            {
                foreach (var client in clients)
                {
                    client.SendData(data);
                }
                /*foreach (var pb in TCPData)
                {
                    Console.Write(pb + " ");
                }
                Console.WriteLine("");*/
            }
        }

        public override void ClosePort()
        {
            foreach(var client in clients)
            {
                client.CloseClient();
            }
            clients.Clear();
        }
    }


}
