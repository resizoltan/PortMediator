using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Ports;

namespace PortMediator
{
    public class SerialPort : Port
    {
        System.IO.Ports.SerialPort port;
        Task readingTask = null;
        CancellationTokenSource readingTaskCancellationTokenSource = new CancellationTokenSource();


        public SerialPort(string portName)
        {
            port = new System.IO.Ports.SerialPort(portName);
            port.BaudRate = 115200;
            port.Parity = Parity.None;
            port.ReadBufferSize = 100;
        }

        public override void Close()
        {
            try
            {
                port.Close();
            }
            catch (Exception e)
            {
                e.Source = GetID();
                throw e;
            }
        }

        public override Task<bool> Open(string portName, Client client)
        {
            Task<bool> openTask = Task<bool>.Run(delegate
            {
                bool success = false;
                try
                {
                    port.Open();
                    success = port.IsOpen;
                }
                catch (Exception e)
                {
                    e.Source = GetID();
                    success = false;
                    throw e;
                }
                return success;
            });
           
            return openTask;
        }

        public override void SendData(byte[] data)
        {
            try
            {
                port.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                e.Source = GetID();
                throw e;
            }
        }

        public override Task<bool> StartReading()
        {
            Task<bool> startReadingTask = Task<bool>.Factory.StartNew(delegate
            {
                bool success = true;
                try
                {
                    byte[] buffer = new byte[port.ReadBufferSize];
                    Action Read = async delegate
                    {
                        while (port.IsOpen || !readingTaskCancellationTokenSource.IsCancellationRequested)
                        {
                            int dataLength = await port.BaseStream.ReadAsync(buffer, 0, 1);
                            byte[] data = new byte[dataLength];
                            Array.Copy(buffer, data, dataLength);
                            if (dataLength != 1)
                            {
                                Console.WriteLine("WARNING: serial port read data size is " + dataLength + ", data skipped. " + GetID());
                            }
                            else
                            {
                                OnDataReceived(data);
                            }
                        }
                    };
                    readingTask = Task.Factory.StartNew(Read, readingTaskCancellationTokenSource.Token);
                }
                catch (Exception e)
                {
                    e.Source = GetID();
                    success = false;
                    throw e;
                }
                return success;
            });
            
            return startReadingTask;
        }

        public override void StopReading(Client client)
        {
            if((readingTask.Status == TaskStatus.Running) ||
                (readingTask.Status == TaskStatus.WaitingForActivation) || 
                (readingTask.Status == TaskStatus.WaitingForChildrenToComplete) ||
                (readingTask.Status == TaskStatus.WaitingToRun))
            {
                readingTaskCancellationTokenSource.Cancel();
            }
        }

        public override string GetID()
        {
            return "SerialPort " + port.PortName;
        }
    }

    class SerialPeripheral : Peripheral
    {
        
        //public static string[] defaultPortNames { get; set; } = { "COM8", "COM13" };
        public Dictionary<string, SerialPort> defaultPorts = new Dictionary<string, SerialPort>
        {
            ["COM8"] = new SerialPort("COM8"),
            ["COM13"] = new SerialPort("COM13")
        };
        //private static int nextPortIndex = 0;

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

        public override Task<bool> OpenPort(string portID, Client client)
        {
            try
            {
                ports.Add(client, defaultPorts[portID]);
            }
            catch(Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return openTask;
        }

        public async Task<bool> OpenPort(string port)
        {
            
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
            
        }

        public override void StopReadingPort(Client client)
        {
            client.canReceive = false;
        }

        public override Task<bool> StartPeripheral()
        {
            isRunning = true;
            Task<bool> openTask = Task<bool>.Factory.StartNew(delegate
            {
                bool success = true;
                foreach (SerialPort port in defaultPorts.Values)
                {
                    port.BaudRate = 115200;
                    port.Parity = Parity.None;
                    port.ReadBufferSize = 100;

                    try
                    {
                        port.Open();
                        success &= port.IsOpen;
                        if (port.IsOpen)
                        {
                            port.DiscardInBuffer();
                            port.DiscardOutBuffer();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Serial: " + port + ": " + e.Message);
                        Console.WriteLine("Could not start port " + port);
                        success = false;
                    }
                }
                return success;
            });

            return openTask;
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
