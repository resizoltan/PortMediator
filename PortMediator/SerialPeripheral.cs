using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace PortMediator
{
    class SerialPeripheral : Peripheral
    {
        public static string[] comPorts { get; set; } = { "COM8", "COM13" };
        private static int nextPortIndex = 0;

        Dictionary<Client, SerialPort> ports = new Dictionary<Client, SerialPort>();

        public override void ClosePort(Client client)
        {
            try
            {
                client.SendCloseSignal();
                ports[client].Close();
            }
            catch(Exception e)
            {
                Console.WriteLine("Port to " + client.name + " of type " + client.type + " is already closed");
            }
            throw new NotImplementedException();
        }

        public override Task<bool> OpenPort(Client client)
        {
            Task<bool> openTask = OpenPort(client, comPorts[nextPortIndex]);
            nextPortIndex++;
            return openTask;
        }

        public async Task<bool> OpenPort(Client client, string port)
        {
            SerialPort serialPort = new SerialPort();
            serialPort.PortName = port;
            serialPort.BaudRate = 115200;
            serialPort.Parity = Parity.None;
            serialPort.ReadBufferSize = 100;
            bool success = await Task<bool>.Run(() =>
            {
                bool isOpen = false;
                try
                {
                    serialPort.Open();
                    isOpen = serialPort.IsOpen;
                }
                catch(Exception e)
                {
                    Console.WriteLine("Serial: " + port + ": " + e.Message);
                    Console.WriteLine("Could not open port to " + client.name + " of type " + client.type);
                    isOpen = false;
                }
                return isOpen;

            });
            if (success)
            {
                serialPort.DiscardInBuffer();
                serialPort.DiscardOutBuffer();
            }
            
            return success;
        }

        public override void SendData(Client client, byte[] data)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StartPeripheral()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StartReadingPort(Client client)
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StopPeripheral()
        {
            throw new NotImplementedException();
        }

        public override void StopReadingPort(Client client)
        {
            throw new NotImplementedException();
        }
    }
}
