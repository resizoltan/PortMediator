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
       
        public SerialPort(string portName, Action<Client> NewClientHandler) : base(NewClientHandler)
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
            catch (AggregateException e)
            {
                e.Source = "PortMediator.SerialPort.Close() of " + GetID() + " -> " + e.Source;
                throw e;
            }
        }

        public override void Open()
        {
            try
            {
                port.Open();
                StartWaitingForConnectionRequest();

            }
            catch (Exception e)
            {
                e.Source = "PortMediator.SerialPort.Open() of " + GetID() + " -> " + e.Source;
                throw e;
            }
        }

        public override Task SendData(byte[] data)
        {
            try
            {
                Task writeTask = Task.Factory.StartNew(delegate
                {
                    port.Write(data, 0, data.Length);
                });
                return writeTask;
            }
            catch (Exception e)
            {
                e.Source = "PortMediator.SerialPort.SendData() of " + GetID() + " -> " + e.Source;
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
            try
            {
                readingTask = Task.Factory.StartNew(Read, 
                    readingTaskCTS.Token,
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch (AggregateException e)
            {
                e.Source = "PortMediator.SerialPort.StartReading() of " + GetID() + " -> " + e.Source;
                throw e;
            }
            
        }

        private async void Read()
        {
            byte[] buffer = new byte[port.ReadBufferSize];
            while (port.IsOpen && !readingTaskCTS.IsCancellationRequested)
            {
                try
                {
                    int dataLength = await port.BaseStream.ReadAsync(buffer, 0, 1);
                    byte[] data = new byte[dataLength];
                    Array.Copy(buffer, data, dataLength);
                    OnDataReceived(data);
                }
                catch (AggregateException e)
                {
                    Console.WriteLine(GetID() + e.Message);
                }

            }
        }

        public override void StopReading(Client client)
        {
            if( (readingTask.Status == TaskStatus.Running) ||
                (readingTask.Status == TaskStatus.WaitingForActivation) || 
                (readingTask.Status == TaskStatus.WaitingForChildrenToComplete) ||
                (readingTask.Status == TaskStatus.WaitingToRun))
            {
                readingTaskCTS.Cancel();
            }
        }

        public override string GetID()
        {
            return "SerialPort " + port.PortName;
        }

        public override void StartWaitingForConnectionRequest()
        {
            if (!port.IsOpen)
            {
                Exception e =  new Exception("Port closed");
                e.Source = "StartWaitingForConnectionRequest()" ;
                throw e;
            }
            try
            {
                readingTask = Task.Factory.StartNew(MonitorPort, 
                    waitForConnectionRequestTaskCTS.Token, 
                    TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            catch (AggregateException e)
            {
                e.Source = "WaitForConnectionRequest() -> " + e.Source;
                throw e;
            }
        }

        private async void MonitorPort()
        {
            byte[] buffer = new byte[port.ReadBufferSize];
            byte[] data = new byte[connectionRequestMessageLength];
            int bytesRead = 0;

            while (bytesRead != connectionRequestMessageLength)
            {
                if (!port.IsOpen)
                {
                    Exception e = new Exception("Port closed");
                    e.Source = "MonitorPort()";
                    throw e;
                }
                if (waitForConnectionRequestTaskCTS.IsCancellationRequested)
                {
                    Exception e = new Exception("Waiting for connection request canceled");
                    e.Source = "MonitorPort()";
                    throw e;
                }
                try
                {
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
                catch(AggregateException e)
                {
                    Console.WriteLine(GetID() + e.Message);
                }

            }
            ConnectionRequested(this, data);

        }
    }

    class SerialPeripheral : Peripheral
    {
        
        public SerialPeripheral(Action<Client> NewClientHandler) :base(NewClientHandler)
        {
            ports.Add(new SerialPort("COM8", NewClientHandler));
            ports.Add(new SerialPort("COM13", NewClientHandler));
        }

        public override void Start()
        {
            foreach (SerialPort port in ports)
            {
                try
                {
                    port.Open();
                }
                catch (AggregateException e)
                {
                    Console.WriteLine("Error occured in SerialPeripheral.Start(), could not open port");
                    Console.WriteLine("\tError source: " + e.Source);
                    Console.WriteLine("\tError message: " + e.Message);
                }
            }
        }



        //public override void Close()
        //{

        //}

    }
}
