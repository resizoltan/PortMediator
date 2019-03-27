﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

namespace PortMediator
{
    class Program
    {
        static void Main(string[] args)
        {
            string userInput;

            Console.WriteLine("Port Mediator");
            Console.WriteLine("Activating default link");

            Link link = CreateWaldlaeuferLink();
            Task<bool> openLinkTask = link.Open();
            try
            {
                openLinkTask.Wait();
            }
            catch (AggregateException e)
            {
                Console.WriteLine("ERROR in main: " + e.Message);
            }
            if (openLinkTask.Status == TaskStatus.RanToCompletion)
            {
                if(openLinkTask.Result == true)
                {
                    Task<bool> activateLinkTask = link.Activate();
                    try
                    {
                        activateLinkTask.Wait();
                    }
                    catch(AggregateException e)
                    {
                        Console.WriteLine("ERROR in main: " + e.Message);
                    }

                    if (activateLinkTask.Status == TaskStatus.RanToCompletion)
                    {
                        if (activateLinkTask.Result == true)
                        {
                            Console.WriteLine("Link active");
                        }
                        else
                        {
                            Console.WriteLine("ActivateLinkTask returned false");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Failed to activate link: " + activateLinkTask.Status);
                        foreach (var e in activateLinkTask.Exception.InnerExceptions)
                        {
                            Console.WriteLine("Exception: " + e.Message);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("OpenLinkTask returned false");
                }
            }
            else
            {
                Console.WriteLine("Failed to open link: " + openLinkTask.Status);
                foreach(var e in openLinkTask.Exception.InnerExceptions)
                {
                    Console.WriteLine("Exception: " + e.Message);
                }
            }

            do
            {
                userInput = Console.ReadLine();
            } while (userInput != "exit");
        }

        enum PORTS
        {
            mouse,
            matlab,
            bootcommander
        }

        private static Link CreateWaldlaeuferLink()
        {
            Link link = new Link("Waldlaeufer");

            link.AddPort((int)PORTS.mouse, new BLEPort("JDY-10-V2.4"));
            link.AddPort((int)PORTS.bootcommander, new SERIALPort("COM8", 115200));

            link.AddPacketProcessorFunc((int)PORTS.mouse, link.SendDataTo((int)PORTS.bootcommander));
            link.AddPacketProcessorFunc((int)PORTS.bootcommander, link.SendDataTo((int)PORTS.mouse));

            return link;
        }
    }

    class PortHandler
    {
        private Port port_;

        private bool packetInProgress = false;
        private byte packetLength = 0;
        private byte[] packet = null;
        private byte bytesReceived = 0;

        private void ProcessData(byte[] data)
        {
            foreach(byte b in data)
            {
                if (!packetInProgress)
                {
                    packetInProgress = true;
                    packetLength = b;
                    packet = new byte[packetLength + 1];
                    packet[0] = packetLength;
                }
                else
                {
                    packet[++bytesReceived] = b;
                    if (bytesReceived == packetLength)
                    {
                        processPacket(packet);
                        packetInProgress = false;
                        packetLength = 0;
                        bytesReceived = 0;
                    }
                }
            }
        }

        private Action<byte[]> processPacket = null;
        public Action<byte[]> ProcessPacket {
            set
            {
                processPacket = value;
            }
        }

        

        public PortHandler(Port port)
        {
            port_ = port;
            port_.DataReceived += (object sender, DataReceivedEventArgs eventArgs) => ProcessData(eventArgs.data);
        }

        public PortHandler(Port port, Action<byte[]> packetProcessorFunc)
        {
            port_ = port;
            ProcessPacket = packetProcessorFunc;
        }

        public Task<bool> OpenPort()
        {
            return port_.OpenPort();
        }

        public Task<bool> StartReading()
        {
            return port_.StartReading();
        }

        public void Close()
        {
            port_.ClosePort();
        }

        public void SendData(byte[] data)
        {
            port_.SendData(data);
        }
    }
    class Link
    {
        string name_;
        
        protected Dictionary<int, PortHandler> ports = new Dictionary<int, PortHandler>();

        public Link(string name)
        {
            name_ = name;
        }

        public void AddPort(int ID, Port type)
        {
            ports.Add(ID, new PortHandler(type));
        }

        public void AddPacketProcessorFunc(int portID, Action<byte[]> packetProcessorFunc)
        {
            try
            {
                PortHandler foundPort = ports[portID];
                foundPort.ProcessPacket = packetProcessorFunc;
            }
            catch (KeyNotFoundException e)
            {
                Console.WriteLine("ERROR: Non-existent port requested");
            }
            catch (NullReferenceException e)
            {
                Console.WriteLine("ERROR: " + e.Message);
            }
        }

        public Action<byte[]> SendDataTo(int portID)
        {
            
            Action<byte[]> dataSenderFunc = null;
            try
            {
                PortHandler foundPort = ports[portID];
                dataSenderFunc = foundPort.SendData;
            }
            catch(KeyNotFoundException e)
            {
                Console.WriteLine("ERROR: Non-existent port requested");
                dataSenderFunc = null;
            }
            catch(NullReferenceException e)
            {
                Console.WriteLine("ERROR: " + e.Message);
            }

            return dataSenderFunc;
        }

        public async Task<bool> Open()
        {
            bool success = true;
            foreach(var port in ports)
            {
                success &= await port.Value.OpenPort();
            }
            return success;
        }

        public async Task<bool> Activate()
        {
            bool success = true;
            foreach (var port in ports)
            {
                success &= await port.Value.StartReading();
            }
            return success;
        }

        public void Close()
        {
            foreach (var port in ports)
            {
                port.Value.Close();
            }
        }

    }
}
