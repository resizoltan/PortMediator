using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    class Channel
    {
        enum DIRECTION
        {
            ONEWAY,
            TWOWAY
        }
        DIRECTION direction = DIRECTION.TWOWAY;
        Client client1 = null;
        Client client2 = null;

        Filter filter12 { get; set; } = null;
        Filter filter21 { get; set; } = null;

        public Channel(Client client1, Client client2)
        {
            this.client1 = client1;
            this.client2 = client2;
            this.filter12 = Filter.CreateNewNonBlocking();
            this.filter21 = Filter.CreateNewNonBlocking();
            client1.DataReceived += Client1Listener;
            client2.DataReceived += Client2Listener;
        }

        //public Channel(Client client1, Client client2, Filter filter12, Filter filter21)
        //{
        //    this.client1 = client1;
        //    this.client2 = client2;
        //    this.filter12 = filter12;
        //    this.filter21 = filter21;
        //}

        public static Channel CreateTwoWay(Client client1, Client client2)
        {
            Channel channel = new Channel(client1, client2);
            return channel;
        }

        public static Channel CreateOneWay(Client fromClient1, Client toClient2)
        {
            Channel channel = new Channel(fromClient1, toClient2);
            channel.direction = DIRECTION.ONEWAY;
            return channel;
        }

        public void Client1Listener(object sender, PacketReceivedEventArgs eventArgs)
        {
            Communication.Packet packet = eventArgs.packet;
            try
            {
                if (filter12.FilterPacket(packet) == false)
                {
                    client2.SendData(packet);
                }
            }
            catch(Exception e)
            {
                Console.WriteLine("Error occured in Channel.Client1Listener() during packet transfer from " + client1.name + " to " + client2.name);
                Console.WriteLine("\tError source:  " + e.Source);
                Console.WriteLine("\tError message: " + e.Message);
                Console.WriteLine("\tPacket discarded");
            }
        }

        public void Client2Listener(object sender, PacketReceivedEventArgs eventArgs)
        {
            if(direction == DIRECTION.TWOWAY)
            {
                Communication.Packet packet = eventArgs.packet;
                try
                {
                    if (filter21.FilterPacket(packet) == false)
                    {
                        client1.SendData(packet);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error occured in Channel.Client2Listener() during packet transfer from " + client1.name + " to " + client2.name);
                    Console.WriteLine("\tError source:  " + e.Source);
                    Console.WriteLine("\tError message: " + e.Message);
                    Console.WriteLine("\tPacket discarded");
                }
            }
        }
    }

    class Filter
    {
        Dictionary<Communication.COMMAND, bool> blockedCommands = null;
        Dictionary<Communication.VERBOSITY, bool> blockedVerbosities = null;

        public bool FilterPacket(Communication.Packet packet)
        {
            bool blockPacket = false;

            try
            {
                Communication.COMMAND command = Communication.GetCommand(packet);

                if (command == Communication.COMMAND.TEXT)
                {
                    Communication.VERBOSITY verbosity = Communication.GetVerbosity(packet);
                    if (blockedVerbosities[verbosity] == true)
                    {
                        blockPacket = true;
                    }
                }

                if (blockedCommands[command] == true)
                {
                    blockPacket = true;
                }
            }
            catch(Exception e)
            {
                e.Source = "Filter.FilterPacket() -> " + e.Source;
                throw e;
            }

            return blockPacket;
        }

        public Filter(List<Communication.COMMAND> blockedCommands, List<Communication.VERBOSITY> blockedVerbosities)
        {
            this.blockedCommands = new Dictionary<Communication.COMMAND, bool>();
            for (int command = 0; command < (int)Communication.COMMAND.COMMANDCOUNT; command++)
            {
                this.blockedCommands.Add((Communication.COMMAND)command, false);
            }
            foreach(Communication.COMMAND command in blockedCommands)
            {
                this.blockedCommands[command] = true;
            }

            this.blockedVerbosities = new Dictionary<Communication.VERBOSITY, bool>();
            for (int verbosity = 0; verbosity < (int)Communication.VERBOSITY.VERBOSITYCOUNT; verbosity++)
            {
                this.blockedVerbosities.Add((Communication.VERBOSITY)verbosity, false);
            }
            foreach (Communication.VERBOSITY verbosity in blockedVerbosities)
            {
                this.blockedVerbosities[verbosity] = true;
            }
        }

        public static Filter CreateNewNonBlocking()
        {
            return new Filter(new List<Communication.COMMAND>(), new List<Communication.VERBOSITY>());
        }
    }

    
   
}
