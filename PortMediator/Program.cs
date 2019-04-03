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
        static Peripheral BLEPeripheral = new BLEPeripheral();

        static Dictionary<Client.TYPE, List<Client>> clientsByType = null;
        static List<Channel> channels = null;
        static Dictionary<Client.TYPE, List<Client.TYPE>> dataFlowRules = null;
        //static Dictionary<Client, List<Client>> dataDestinations = new Dictionary<Client, List<Client>>();
        //static Dictionary<Client, Action<byte[]>> dataListeners = new Dictionary<Client, Action<byte[]>>();
        static Action<Client> dataFlowAddClient = null;

        static Client[] clients
        {
            get
            {
                int clientCount = 0;
                foreach (List<Client> clientList in clientsByType.Values)
                {
                    clientCount += clientList.Count;
                }
                Client[] clientArray = new Client[clientCount];
                int destinationIndex = 0;
                foreach (List<Client> clientList in clientsByType.Values)
                {
                    Array.Copy(clientList.ToArray(), 0, clientArray, destinationIndex, clientList.Count);
                    destinationIndex += clientList.Count;
                }
                return clientArray;
            }
        }

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

            dataFlowRules = DataFlowRules.CreateNew(DataFlowRules.ConsoleAndMatlab);

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

            public static Dictionary<Client.TYPE, List<Client.TYPE>> ConsoleAndMatlab = new Dictionary<Client.TYPE, List<Client.TYPE>>
            {
                [Client.TYPE.CONSOLE] = new List<Client.TYPE> { Client.TYPE.MATLAB },
                [Client.TYPE.MATLAB] = new List<Client.TYPE> { Client.TYPE.CONSOLE },
                [Client.TYPE.MATLAB] = new List<Client.TYPE> { Client.TYPE.MATLAB },
                [Client.TYPE.CONSOLE] = new List<Client.TYPE> { Client.TYPE.CONSOLE }
            };
        }
      

        static void Run()
        {
            Console.WriteLine("PortMediator v2");
            Console.WriteLine("Opening peripherals...");
            OpenAll();

            string input;
            do
            {
                input = Console.ReadLine();
            } while (input != "exit");


            CloseAll();


        }

        static void OpenAll()
        {
            serialPeripheral.NewClientReceived += NewClientCallback;
            serialPeripheral.Start();
            BLEPeripheral.NewClientReceived += NewClientCallback;
            BLEPeripheral.Start();
        }

        static void CloseAll()
        {
            //foreach(Client client in clients)
            //{
            //    client.SendCloseSignal();
            //}
            serialPeripheral.Close();
            BLEPeripheral.Close();
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
            foreach(List<Client> clientList in clientsByType.Values)
            {
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
    }
}
