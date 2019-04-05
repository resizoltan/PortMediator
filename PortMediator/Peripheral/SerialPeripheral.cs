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

        public override void Open()
        {
            port.Open();
            StartWaitingForConnectionRequest();
        }

        public override void Close()
        {
            if (port.IsOpen)
            {
                port.Close();
            }
        }


        public override void SendData(byte[] data)
        {
            try
            {
                sendTask = port.BaseStream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception e)
            {
                e.Source = "PortMediator.SerialPort.SendData() of " + ID + " -> " + e.Source;
                throw e;
            }
        }

        public override void StartReading()
        {
            if (!port.IsOpen)
            {
                Exception e = new Exception("Port closed");
                e.Source = "StartReading()";
                throw e;
            }

            waitForClientConnectionTask = Read();


        }

        private async Task Read()
        {
            byte[] buffer = new byte[port.ReadBufferSize];
            while (port.IsOpen && !readTaskCTS.IsCancellationRequested)
            {
                try
                {
                    int dataLength = await port.BaseStream.ReadAsync(buffer, 0, 1);
                    byte[] data = new byte[dataLength];
                    Array.Copy(buffer, data, dataLength);
                    OnDataReceived(data);
                }
                catch (Exception e)
                {
                    e.Source = "PortMediator.SerialPort.Read() of " + ID + " -> " + e.Source;
                    throw e;
                }
            }
        }

        public override void StopReading(Client client)
        {
            if( (WaitForConnectionRequestTask.Status == TaskStatus.Running) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForActivation) || 
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForChildrenToComplete) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingToRun))
            {
                readTaskCTS.Cancel();
            }
        }

        public override string ID
        {
            get
            {
                return "SerialPort " + port.PortName;
            }
        }

        public override void StartWaitingForConnectionRequest()
        {
            if (!port.IsOpen)
            {
                Exception e =  new Exception("Port closed");
                e.Source = "StartWaitingForConnectionRequest()" ;
                throw e;
            }

            waitForClientConnectionTask = MonitorPort();

        }

        private async Task MonitorPort()
        {
            byte[] buffer = new byte[port.ReadBufferSize];
            byte[] data = new byte[connectionRequestMessageLength];
            int bytesRead = 0;

            while (bytesRead != connectionRequestMessageLength && 
                port.IsOpen && 
                !waitForConnectionRequestTaskCTS.IsCancellationRequested)
            {
                //try
                //{
                    int dataLength = await port.BaseStream.ReadAsync(buffer, 0, connectionRequestMessageLength);
                    if (dataLength <= connectionRequestMessageLength - bytesRead)
                    {
                        Array.Copy(buffer, 0, data, bytesRead, dataLength);
                        bytesRead += dataLength;
                    }
                    else
                    {
                        Array.Copy(buffer, 0, data, bytesRead, connectionRequestMessageLength - bytesRead);
                        bytesRead = connectionRequestMessageLength;
                    }
                //}
                //catch(AggregateException e)
                //{
                //    e.InnerException.Source = "Port.MonitorPort() of " + GetID() + " -> " + e.Source;
                //    throw e.InnerException;
                //}
                
            }

            if(bytesRead == connectionRequestMessageLength)
            {
                OnConnectionRequest(this, data);
            }
        }
    }

    class SerialPeripheral : Peripheral
    {
        static readonly string[] defaultSerialPortNames = { "COM8", "COM13" };

        public override string ID {
            get
            {
                return "SerialPeripheral";
            }
        }

        public override void Start()
        {
            foreach (string portName in defaultSerialPortNames)
            {
                Port port = new SerialPort(portName);
                //port.PortClosed += PortClosedEventHandler;
                port.Open();
                PortRequestedEventArgs eventArgs = new PortRequestedEventArgs(port);
                OnePortRequested(eventArgs);
            }
        }

        public override void Stop()
        {

        }

        //protected override void PortClosedEventHandler(object sender, PortClosedEventArgs eventArgs)
        //{
            
        //}
    }
}
