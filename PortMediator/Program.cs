using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace PortMediator
{
    class Program
    {
        //static Program program = new Program();

        static Peripheral serialPeripheral = new SerialPeripheral();

        static Dictionary<Client.TYPE, List<Client>> clientsByType = null;
        static List<Channel> channels = null;
        static Dictionary<Client.TYPE, List<Client.TYPE>> dataFlowRules = null;
        //static Dictionary<Client, List<Client>> dataDestinations = new Dictionary<Client, List<Client>>();
        //static Dictionary<Client, Action<byte[]>> dataListeners = new Dictionary<Client, Action<byte[]>>();
        static Action<Client> dataFlowAddClient = null;

        static void Main(string[] args)
        {
            Program.Init();
            Program.Run();
            
        }

        static void Init()
        {
            clientsByType = new Dictionary<Client.TYPE, List<Client>>();
            for (int type = 0; type < (int)Client.TYPE.TYPECOUNT; type++)
            {
                clientsByType.Add((Client.TYPE)type, new List<Client>());
            }

            channels = new List<Channel>();

            dataFlowRules = DataFlowRules.CreateNew(DataFlowRules.ConsoleToConsole);

        }
        static class DataFlowRules
        {

            public static Dictionary<Client.TYPE, List<Client.TYPE>> CreateNew(Dictionary<Client.TYPE, List<Client.TYPE>> initRules)
            {
                Dictionary<Client.TYPE,List<Client.TYPE>> dataFlowRules = new Dictionary<Client.TYPE, List<Client.TYPE>>();
                for (int type = 0; type < (int)Client.TYPE.TYPECOUNT; type++)
                {
                    dataFlowRules.Add((Client.TYPE)type, new List<Client.TYPE>());
                }
                Init(dataFlowRules, initRules);
                return dataFlowRules;
            }

            public static void Init(Dictionary<Client.TYPE, List<Client.TYPE>> dataFlowRules, Dictionary<Client.TYPE, List<Client.TYPE>> initRules)
            {
                foreach(var rule in initRules)
                {
                    dataFlowRules[rule.Key] = rule.Value;
                }
            }

            public static Dictionary<Client.TYPE, List<Client.TYPE>> ConsoleToConsole = new Dictionary<Client.TYPE, List<Client.TYPE>>
            {
                [Client.TYPE.CONSOLE] = new List<Client.TYPE> { Client.TYPE.CONSOLE }
            };
        }
       

        //static void DataFlowRuleAllToAll(Client newClient)
        //{
        //    if (!dataDestinations.ContainsKey(newClient))
        //    {
        //        dataDestinations.Add(newClient, new List<Client>());
        //        foreach (List<Client> clientList in clientsByType.Values)
        //        {
        //            foreach(Client client in clientList)
        //            {
        //                if(client != newClient)
        //                {
        //                    Channel newChannel
        //                    dataDestinations[newClient].Add(client);
        //                    dataDestinations[client].Add(newClient);
        //                }
        //            }
        //        }
        //    }
        //}

        static void Run()
        {
            Console.WriteLine("PortMediator v2");
            Console.WriteLine("Opening peripherals...");
            serialPeripheral.NewClientReceived += NewClientCallback;
            serialPeripheral.StartPeripheral();
            
            string input;
            do
            {
                input = Console.ReadLine();
            } while (input != "exit");

            serialPeripheral.ClosePeripheral();
        }

        static void NewClientCallback(object sender, NewClientEventArgs eventArgs)
        {
            Client newClient = eventArgs.client;
            AddClient(newClient);
            newClient.StartReading();

            Console.WriteLine("New Client: " + newClient.name);
        }

        static void AddClient(Client newClient)
        {
            //List<Client.TYPE> subscribingClientTypes = dataFlowRules[newClient.type];
            foreach(List<Client> clientList in clientsByType.Values)
            {
                //List<Client> subscribingClients = clientsByType[clientType];
                foreach(Client client in clientList)
                {
                    bool isSubscribingToNew = dataFlowRules[newClient.type].Contains(client.type);
                    bool isNewSubscribing = dataFlowRules[client.type].Contains(newClient.type);
                    Channel channel = null;
                    if (isSubscribingToNew && isNewSubscribing)
                    {
                        channel = Channel.CreateTwoWay(newClient, client);
                    }
                    else if(isSubscribingToNew)
                    {
                        channel = Channel.CreateOneWay(client, newClient);
                    }
                    else if (isNewSubscribing)
                    {
                        channel = Channel.CreateOneWay(newClient, client);
                    }
                    if(channel != null)
                    {
                        channels.Add(channel);
                    }
                }
            }
            clientsByType[newClient.type].Add(newClient);
        }
        //static void removeDataReceivedCallbacks()
        //{
        //    foreach (var destinationClients in dataDestinations)
        //    {
        //        Client sourceClient = destinationClients.Key;
        //        foreach (Client destinationClient in destinationClients.Value)
        //        {
        //            try
        //            {
        //                sourceClient.DataReceived -= (object sender, DataReceivedEventArgs eventArgs) => { destinationClient.SendData(eventArgs.data); };
        //            }
        //            catch(Exception e)
        //            {
        //                Console.WriteLine("Error during removing SendDataFunc of " + destinationClient.name + " from " + sourceClient.name);
        //                Console.WriteLine(e.Message);
        //            }
        //        }
        //    }
        //}

        //static void addDataReceivedCallbacks()
        //{
        //    foreach(var destinationClients in dataDestinations)
        //    {
        //        Client sourceClient = destinationClients.Key;
        //        foreach (Client destinationClient in destinationClients.Value)
        //        {
        //            try
        //            {
        //                sourceClient.DataReceived += (object sender, DataReceivedEventArgs eventArgs) => { destinationClient.SendData(eventArgs.data); };
        //            }
        //            catch (Exception e)
        //            {
        //                Console.WriteLine("Error during adding SendDataFunc of " + destinationClient.name + " to " + sourceClient.name);
        //                Console.WriteLine(e.Message);
        //            }
        //        }
        //    }
        //}

    }
}
