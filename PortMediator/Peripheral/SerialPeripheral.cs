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
            //catch (AggregateException ae)
            //{
            //    foreach(Exception e in ae.InnerExceptions)
            //    {
            //        throw e;
            //    }
            //}
            catch(Exception e)
            {
                throw new PortException(this, e);
            }
        }

        public override void StartReading()
        {
            if (!port.IsOpen)
            {
                PortClosedException e = new PortClosedException(this);
                throw e;
            }
            waitForClientConnectionTask = Read();
        }

        private async Task Read()
        {
            try
            {
                byte[] buffer = new byte[port.ReadBufferSize];
                while (port.IsOpen)
                {
                    int dataLength = await port.BaseStream.ReadAsync(buffer, 0, 1);
                    byte[] data = new byte[dataLength];
                    Array.Copy(buffer, data, dataLength);
                    BytesReceivedEventArgs eventArgs = new BytesReceivedEventArgs(data);
                    OnDataReceived(eventArgs);
                }
            }
            catch (Exception e)
            {
                ExceptionOccuredEventArgs eventArgs = new ExceptionOccuredEventArgs(e);
                OnExceptionOccured(eventArgs);
            }
           
        }

        public override void StopReading(Client client)
        {
            if( (WaitForConnectionRequestTask.Status == TaskStatus.Running) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForActivation) || 
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingForChildrenToComplete) ||
                (WaitForConnectionRequestTask.Status == TaskStatus.WaitingToRun))
            {
                Close();
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
                throw new PortClosedException(this/*, "StartWaitingForConnectionRequest()"*/);
            }
            waitForClientConnectionTask = MonitorPort();
        }

        private async Task MonitorPort()
        {
            byte[] buffer = new byte[port.ReadBufferSize];
            byte[] data = new byte[connectionRequestMessageLength];
            int bytesRead = 0;

            while (bytesRead != connectionRequestMessageLength)
            {
                try
                {
                    if (!port.IsOpen)
                    {
                        throw new PortClosedException(this);
                    }
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
                }
                //catch(AggregateException ae)
                //{
                //    foreach(Exception e in ae.InnerExceptions)
                //    {
                //        throw e;
                //    }
                //}
                catch (Exception e)
                {
                    //e.Source = "MonitorPort()";
                    ExceptionOccuredEventArgs eventArgs = new ExceptionOccuredEventArgs(e);
                    OnExceptionOccured(eventArgs);
                    break;
                }

            }

            if(bytesRead == connectionRequestMessageLength)
            {
                ConnectionRequestedEventArgs eventArgs = new ConnectionRequestedEventArgs(data);
                OnConnectionRequest(eventArgs);
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
