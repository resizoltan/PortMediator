using System;
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
            openLinkTask.Wait();
            if (openLinkTask.Status == TaskStatus.RanToCompletion)
            {
                if(openLinkTask.Result == true)
                {
                    Task<bool> activateLinkTask = link.Activate();
                    activateLinkTask.Wait();

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

        private static Link CreateWaldlaeuferLink()
        {
            Link link = new Link("WaldlaeuferLink");

            string mouse = "mouse";
            string matlab = "matlab";
            string bootcommander = "bootcommander";

            link.AddPort(mouse, new BLEPort("JDY-10-V2.4"), link.DataSenderFunc(bootcommander));
            link.AddPort(bootcommander, new SERIALPort("COM9", 115200), (byte[] data) =>
            {
                if (!packetInProgress)
                {
                    packetInProgress = true;
                    packetLength = data;
                    packet = new byte[packetLength + 1];
                    packet[0] = packetLength;
                }
                else
                {
                    packet[++bytesReceived] = data;
                    if (bytesReceived == packetLength)
                    {
                        port1_.SendData(packet);
                        packetInProgress = false;
                        packetLength = 0;
                        bytesReceived = 0;
                    }
                }
            });


            return link;
        }
    }

    
    class Link
    {
        string name_;
        //public enum PortName

        protected class PortHandler
        {
            private Port port_;
            public Action<byte[]> DataProcessor_ { get; set; }

            public PortHandler(Port port)
            {
                port_ = port;
                port_.DataReceived += (object sender, DataReceivedEventArgs eventArgs) => DataProcessor_(eventArgs.data);
            }

            public PortHandler(Port port, Action<byte[]> dataProcessor)
            {
                port_ = port;
                DataProcessor_ = dataProcessor;
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

        protected Dictionary<string, PortHandler> ports = null;

        /*public abstract void ProcessPortData(object sender, DataReceivedEventArgs eventArgs);
        public abstract void ProcessPort2Data(object sender, DataReceivedEventArgs eventArgs);*/

        public Link(string name)
        {
            name_ = name;
        }

        public void AddPort(string name, Port type, Action<byte[]> dataProcessor)
        {
            ports.Add(name, new PortHandler(type, dataProcessor));
        }

        public Action<byte[]> DataSenderFunc(string portName)
        {
            
            Action<byte[]> dataSenderFunc = null;
            try
            {
                PortHandler foundPort = ports[portName];
                dataSenderFunc = foundPort.SendData;
            }
            catch(KeyNotFoundException e)
            {
                Console.WriteLine("ERROR: Non-existent port requested");
                dataSenderFunc = null;
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

    /*class WaldlaeuferLink : Link
    {
        public WaldlaeuferLink() : base("WaldlaeuferLink")
        {

        }

        


        bool packetInProgress = false;
        byte packetLength = 0;
        byte[] packet = null;
        byte bytesReceived = 0;
        public override void ProcessPort2Data(object sender, DataReceivedEventArgs eventArgs)
        {
            // data should be 1 byte!
            byte data = eventArgs.data[0];
            if (!packetInProgress)
            {
                packetInProgress = true;
                packetLength = data;
                packet = new byte[packetLength + 1];
                packet[0] = packetLength;
            }
            else
            {
                packet[++bytesReceived] = data;
                if(bytesReceived == packetLength)
                {
                    port1_.SendData(packet);
                    packetInProgress = false;
                    packetLength = 0;
                    bytesReceived = 0;
                }
            }
        }
    }*/
}
