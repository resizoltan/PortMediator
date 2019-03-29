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
            try
            {
                ports[client].Write(data, 0, data.Length);
            }
            catch(Exception e)
            {
                Console.WriteLine("Could not send data to " + client.name + " of type " + client.type);
                Console.WriteLine(e.Message);
            }
        }


        public override Task<bool> StartReadingPort(Client client)
        {
            bool success = true;
            try
            {
                SerialPort port = ports[client];
                byte[] buffer = new byte[port.ReadBufferSize];
                Action StartReading = async delegate
                {
                    client.canSend = true;
                    while (client.canSend)
                    {
                        if (port.IsOpen)
                        {
                            int dataLength = await port.BaseStream.ReadAsync(buffer, 0, 1);
                            byte[] data = new byte[dataLength];
                            Array.Copy(buffer, data, dataLength);
                            if (dataLength != 1)
                            {
                                Console.WriteLine("WARNING: serial port read data size is " + dataLength);
                            }
                            else
                            {
                                client.OnDataReceived(data);
                            }
                        }
                    }
                };
                Task.Factory.StartNew(StartReading);
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not start reading from " + client.name + " of type " + client.type);
                Console.WriteLine(e.Message);
                success = false;
            }
           
            return Task.FromResult(success);
        }

        public override void StopReadingPort(Client client)
        {
            client.canSend = false;
        }

        public override Task<bool> StartPeripheral()
        {
            isRunning = true;
            return Task.FromResult(true);
        }

        public override Task<bool> StopPeripheral()
        {
            isRunning = false;
            return Task.FromResult(true);
        }

        public override void ClosePeripheral()
        {
            foreach(var port in ports)
            {
                port.Key.SendCloseSignal();
                port.Value.Close();
            }
        }

    }
}
