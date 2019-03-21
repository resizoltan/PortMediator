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

            Link link = new OpenBLTLink();
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
    }

    
    abstract class Link
    {
        protected Port port1_;
        protected Port port2_;

        public abstract void ProcessPort1Data(object sender, DataReceivedEventArgs eventArgs);
        public abstract void ProcessPort2Data(object sender, DataReceivedEventArgs eventArgs);

        public Link(Port port1, Port port2)
        {
            port1_ = port1;
            port2_ = port2;
            port1_.DataReceived += ProcessPort1Data;
            port2_.DataReceived += ProcessPort2Data;
        }

        public async Task<bool> Open()
        {
            bool port1Open = await port1_.OpenPort();
            bool port2Open = await port2_.OpenPort();
            return port1Open & port2Open;
        }

        public async Task<bool> Activate()
        {
            bool port1Active = await port1_.StartReading();
            bool port2Active = await port2_.StartReading();
            return port1Active & port2Active;
        }

        public void Close()
        {
            port1_.ClosePort();
            port2_.ClosePort();
        }

    }

    class OpenBLTLink : Link
    {
        public OpenBLTLink():base(new BLEPort(), new SERIALPort("COM8", 115200))
        {   }

        public override void ProcessPort1Data(object sender, DataReceivedEventArgs eventArgs)
        {
            port2_.SendData(eventArgs.data);
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
    }
}
