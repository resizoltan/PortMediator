using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    class ConsoleClient :Client
    {
        public ConsoleClient(string name, Port port):base(TYPE.CONSOLE, name, port)
        {

        }

        public override void SendData(Communication.Packet packet)
        {
            try
            {
                port.Write(packet.rawData);
            }
            catch (AggregateException e)
            {
                e.Source = "Client.SendData() of client " + name + " -> " + e.Source;
                throw e;
            }
        }

        //if received byte by byte, sending order might be messed up
        public override void ProcessReceivedData(object port, BytesReceivedEventArgs eventArgs)
        {
            packetInReceiving = Communication.Packet.CreateNewFromRaw(eventArgs.data, false);
            OnPacketReadyForTransfer(packetInReceiving);
            packetInReceiving.Clear();
        }

    }

    //class LimitedConcurrencyTaskScheduler : TaskScheduler
    //{
    //    [ThreadStatic]
    //    private static bool _currentThreadIsProcessingItems;

    //    private readonly LinkedList<Task> _tasks = new LinkedList<Task>();

    //    private readonly int _maxDegreeOfParallelism = 1;

    //    protected override IEnumerable<Task> GetScheduledTasks()
    //    {
    //        throw new NotImplementedException();
    //    }

    //    protected override void QueueTask(Task task)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}
}
