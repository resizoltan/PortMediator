using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace PortMediator
{
    public abstract class Port
    {
        protected const int connectionRequestMessageLength = 3;
        
        protected Task readTask = null;
        protected Task writeTask = null;
        protected Task waitForClientConnectionTask = null;

        public Task ReadTask {
            get { return readTask; }
        }
        public Task WriteTask {
            get { return writeTask; }
        } 
        public Task WaitForConnectionRequestTask {
            get { return waitForClientConnectionTask; }
        }

        //protected CancellationTokenSource readTaskCTS = new CancellationTokenSource();
        //protected CancellationTokenSource sendTaskCTS = new CancellationTokenSource();
        //protected CancellationTokenSource waitForConnectionRequestTaskCTS = new CancellationTokenSource();

        public event EventHandler<BytesReceivedEventArgs> BytesReceived;
        public event EventHandler<ClientConnectionRequestedEventArgs> ClientConnectionRequested;
        public event EventHandler<PortClosedEventArgs> PortClosed;
        public event EventHandler<ExceptionOccuredEventArgs> ReadTaskExceptionOccured;
        public event EventHandler<ExceptionOccuredEventArgs> WriteTaskExceptionOccured;
        public event EventHandler<ExceptionOccuredEventArgs> WaitForConnectionRequestTaskExceptionOccured;


        public abstract void Open();
        public abstract void Close();
        public abstract void StartReading();
        public abstract void StartWaitingForConnectionRequest();
        public abstract void StopReading(Client client);
        public abstract void Write(byte[] data);

        public abstract string ID { get; }

        public async Task WaitForAllOperationsToComplete()
        {
            try
            {
                await ReadTask;
                await WriteTask;
                await WaitForConnectionRequestTask;
            }
            catch (NullReferenceException) { }

        }

        protected void OnDataReceived(BytesReceivedEventArgs eventArgs)
        {
            EventHandler<BytesReceivedEventArgs> handler = BytesReceived;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnClose(PortClosedEventArgs eventArgs)
        {
            EventHandler<PortClosedEventArgs> handler = PortClosed;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnConnectionRequest(ConnectionRequestedEventArgs eventArgs)
        {
            byte[] bytes = eventArgs.bytes;
            if(bytes != null && bytes.Length == connectionRequestMessageLength)
            {
                try
                {
                    Client.TYPE type = Client.Identify((byte)(bytes[0]));
                    string name = Client.typenames[type] + "_" + Encoding.ASCII.GetString(bytes, 1, 2);
                    Client newClient = Client.CreateNew(type, name, this);
                    ClientConnectionRequestedEventArgs newClientEventArgs = new ClientConnectionRequestedEventArgs(newClient);
                    OnClientConnectionRequested(newClientEventArgs);
                }
                catch(Exception e)
                {
                    ExceptionOccuredEventArgs exceptionOccuredEventArgs = new ExceptionOccuredEventArgs(e);
                    OnWaitForConnectionRequestExceptionOccured(exceptionOccuredEventArgs);
                    StartWaitingForConnectionRequest();
                }
            }
            else
            {
                StartWaitingForConnectionRequest();
            }
        }
        protected void OnClientConnectionRequested(ClientConnectionRequestedEventArgs eventArgs)
        {
            EventHandler<ClientConnectionRequestedEventArgs> handler = ClientConnectionRequested;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnReadExceptionOccured(ExceptionOccuredEventArgs eventArgs)
        {
            EventHandler<ExceptionOccuredEventArgs> handler = ReadTaskExceptionOccured;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnWriteExceptionOccured(ExceptionOccuredEventArgs eventArgs)
        {
            EventHandler<ExceptionOccuredEventArgs> handler = WriteTaskExceptionOccured;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnWaitForConnectionRequestExceptionOccured(ExceptionOccuredEventArgs eventArgs)
        {
            EventHandler<ExceptionOccuredEventArgs> handler = WaitForConnectionRequestTaskExceptionOccured;
            handler?.Invoke(this, eventArgs);
        }

    }

    public abstract class Peripheral
    {
        protected Task listenForPortConnectionsTask = null;
        //protected CancellationTokenSource listenForPortConnectionsTaskCTS = new CancellationTokenSource();

        public event EventHandler<PortRequestedEventArgs> PortRequested;
        public event EventHandler<ExceptionOccuredEventArgs> WaitForPortConnectionsTaskExceptionOccured;

        public abstract void Start();
        public abstract void Stop();

        public abstract string ID { get; }

        protected void OnPortRequested(PortRequestedEventArgs eventArgs)
        {
            EventHandler<PortRequestedEventArgs> handler = PortRequested;
            handler?.Invoke(this, eventArgs);
        }
        protected void OnWaitForPortConnectionsExceptionOccured(ExceptionOccuredEventArgs eventArgs)
        {
            EventHandler<ExceptionOccuredEventArgs> handler = WaitForPortConnectionsTaskExceptionOccured;
            handler?.Invoke(this, eventArgs);
        }

    }

    public class ConnectionRequestedEventArgs : EventArgs
    {
        public byte[] bytes { get; set; }
        public ConnectionRequestedEventArgs(byte[] bytes)
        {
            this.bytes = bytes;
        }
    }

    public class ClientConnectionRequestedEventArgs : EventArgs
    {
        public Client client { get; set; }
        public ClientConnectionRequestedEventArgs(Client client)
        {
            this.client = client;
        }
    }

    public class PacketReceivedEventArgs : EventArgs
    {
        public Communication.Packet packet { get; set; }
        public PacketReceivedEventArgs(Communication.Packet packet)
        {
            this.packet = packet;
        }
    }

    public class BytesReceivedEventArgs : EventArgs
    {
        public byte[] bytes { get; set; }
        public BytesReceivedEventArgs(byte[] bytes)
        {
            this.bytes = bytes;
        }
    }

    public class PortClosedEventArgs : EventArgs
    {
        public string initiator { get; set; }
        public PortClosedEventArgs(string initiator)
        {
            this.initiator = initiator;
        }
    }

    public class PortRequestedEventArgs : EventArgs
    {
        public Port port { get; set; }
        public PortRequestedEventArgs(Port port)
        {
            this.port = port;
        }
    }

    public class ExceptionOccuredEventArgs
    {
        public Exception exception { get; set; }
        public ExceptionOccuredEventArgs(Exception exception)
        {
            this.exception = exception;
        }
    }

    //public class PortException : Exception
    //{
    //    public Port port { get; set; }
    //    public PortException(Port port)
    //    {
    //        this.port = port;
    //    }
    //    public PortException(Port port, string source)
    //    {
    //        this.port = port;
    //        this.Source = source;
    //    }
    //    public PortException(Port port, string source, string message)
    //    {
    //        this.port = port;
    //        this.Source = source;
    //    }
    //    public PortException(Port port, Exception e):base(e.Message, e.InnerException)
    //    {
    //        this.port = port;
    //        this.Source = source;
    //    }
    //}

    ////public class AggregatePortException : AggregateException
    ////{
    ////    public Port port { get; set; }
    ////    public AggregatePortException(Port port, string source, Exception[] e):base(e)
    ////    {
    ////    }
    ////}

    public class PortClosedException : Exception
    {
        public PortClosedException() : base("Port closed") { }
    }


}
