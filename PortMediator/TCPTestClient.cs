using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Net.Sockets;
using System.Net;

namespace PortMediator
{
    class TCPTestClient
    {
        //TCPPort clientPort = new TCPPort(11001);
        TcpClient clientSocket = new TcpClient();

        public TCPTestClient()
        {

        }

        public void StartClient(IPEndPoint hostEndPoint)
        {
            clientSocket.Connect(hostEndPoint);
            StartReading();
            Console.WriteLine("Client connected to " + hostEndPoint.ToString());
        }

        public void SendToHost(byte[] data)
        {
            NetworkStream serverStream = clientSocket.GetStream();
            serverStream.Write(data, 0, data.Length);
            serverStream.Flush();
        }

        public void Read(byte[] data)
        {
            Console.WriteLine("Client received: ");
            //Console.WriteLine(Encoding.ASCII.GetString(data));
            foreach (var pb in data)
            {
                Console.Write(pb.ToString("X") + " ");
            }
            Console.WriteLine("");
        }
        public void StartReading()
        {
            Task.Factory.StartNew(delegate
            {
                NetworkStream inputStream = clientSocket.GetStream();
                byte[] inputBuffer = new byte[1024];

                while (true)
                {
                    int bytesRead = inputStream.Read(inputBuffer, 0, inputBuffer.Length);

                    byte[] data = new byte[bytesRead];
                    Array.Copy(inputBuffer, 0, data, 0, bytesRead);
                    if (bytesRead != 0)
                    {
                        Read(data);
                    }
                    else
                    {
                        Console.WriteLine("Zero bytes read");

                    }
                }
            });
            
        }

    }
}
