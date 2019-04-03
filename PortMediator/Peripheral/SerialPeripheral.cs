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
                if (port.IsOpen)
                {
                    port.Close();
                }
            }
            catch (Exception e)
            {
                e.Source = "PortMediator.SerialPort.Close() of " + GetID() + " -> " + e.Source;
                throw e;
            }
        }

        public override Task<bool> Open(Peripheral serialPeripheral)
        {
            hostingPeripheral = serialPeripheral;

            bool success = false;
            try
            {
                port.Open();
                if (port.IsOpen)
                {
                    Task<bool> waitingStarted = WaitForConnectionRequest();
                    if(waitingStarted.IsCompleted && waitingStarted.Status == TaskStatus.RanToCompletion)
                    {
                        success = true;
                    }
                }
            }
            catch (Exception e)
            {
                e.Source = "PortMediator.SerialPort.Open() of " + GetID() + " -> " + e.Source;
                success = false;
                throw e;
            }
            return Task<bool>.FromResult(success);
        }

        public override void SendData(byte[] data)
        {
            try
            {
                port.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                e.Source = "PortMediator.SerialPort.SendData() of " + GetID() + " -> " + e.Source;
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
                    e.Source = "PortMediator.SerialPort.StartReading() of " + GetID() + " -> " + e.Source;
                    success = false;
                    throw e;
                }
                return success;
            });
            
            return startReadingTask;
        }

        public override void StopReading(Client client)
        {
            if( (readingTask.Status == TaskStatus.Running) ||
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

        public override Task<bool> WaitForConnectionRequest()
        {
            Task<bool> WaitForConnectionRequestTask = Task<bool>.Factory.StartNew(delegate
            {
                bool success = true;
                try
                {
                    byte[] buffer = new byte[port.ReadBufferSize];
                    byte[] data = new byte[3];
                    int bytesRead = 0;
                    Action MonitorPort = async delegate
                    {
                        while (port.IsOpen || !waitForConnectionRequestTaskCancellationTokenSource.IsCancellationRequested)
                        {
                            int dataLength = await port.BaseStream.ReadAsync(buffer, 0, 3);
                            if(dataLength <= 3 - bytesRead)
                            {
                                Array.Copy(buffer, 0, data, bytesRead, dataLength);
                                bytesRead += dataLength;
                            }
                            else
                            {
                                Array.Copy(buffer, 0, data, bytesRead, 3 - bytesRead);
                                bytesRead = 3;
                            }
                            if(bytesRead == 3)
                            {
                                ConnectionRequested(data);
                                break;
                            }
                        }
                    };
                    readingTask = Task.Factory.StartNew(MonitorPort, waitForConnectionRequestTaskCancellationTokenSource.Token);
                }
                catch (Exception e)
                {
                    e.Source = "PortMediator.SerialPort.WaitForConnectionRequest() of " + GetID() + " -> " + e.Source;
                    success = false;
                    throw e;
                }
                return success;
            });

            return WaitForConnectionRequestTask;
        }
    }

    class SerialPeripheral : Peripheral
    {
        
        private Dictionary<string, SerialPort> defaultPorts = new Dictionary<string, SerialPort>
        {
            ["COM8"] = new SerialPort("COM8"),
            ["COM13"] = new SerialPort("COM13")
        };

        public override Task<bool> StartPeripheral()
        {
            isRunning = true;
            Task<bool> StartPeripheralTask = Task<bool>.Factory.StartNew(delegate
            {
                bool success = true;
                foreach (SerialPort port in defaultPorts.Values)
                {
                    try
                    {
                        port.Open(this);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Could not start port " + e.Source + ": " + e.Message);
                        success = false;
                    }
                }
                return success;
            });

            return StartPeripheralTask;
        }

        public override Task<bool> StopPeripheral()
        {
            isRunning = false;
            return Task.FromResult(true);
        }

        public override void ClosePeripheral()
        {
            foreach(SerialPort port in defaultPorts.Values)
            {
                port.OnClose("Peripheral closed");
                port.Close();
            }
        }

    }
}
