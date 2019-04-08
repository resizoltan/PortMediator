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

        static Peripheral serialPeripheral = null;
        static Peripheral blePeripheral = null;
        static Peripheral tcpPeripheral = null;

        static List<Port> unidentifiedPorts = null;

        static Dictionary<Client.TYPE, List<Client>> clientsByType = null;

        //static List<Channel> channels = null;
        static Dictionary<Client.TYPE, List<Client.TYPE>> dataFlowRules = null;
        //static Dictionary<Client, List<Client>> dataDestinations = new Dictionary<Client, List<Client>>();
        //static Dictionary<Client, Action<byte[]>> dataListeners = new Dictionary<Client, Action<byte[]>>();
        //static Action<Client> dataFlowAddClient = null;

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
            bool success = true;
            try
            {
                Program.Init();
            }
            catch (Exception e)
            {
                Util.DisplayExceptionInfo(e, "during initialization");
                success = false;
            }
            if (success)
            {
                Program.Run();
            }

            Console.ReadKey();

        }

        static void Init()
        {
            serialPeripheral = new SerialPeripheral();
            blePeripheral = new BLEPeripheral();
            tcpPeripheral = new TCPPeripheral();

            serialPeripheral.PortRequested += PortRequestedEventHandler;
            blePeripheral.PortRequested += PortRequestedEventHandler;
            tcpPeripheral.PortRequested += PortRequestedEventHandler;

            serialPeripheral.WaitForPortConnectionsTaskExceptionOccured += WaitForPortConnectionsTaskExceptionEventHandler;
            blePeripheral.WaitForPortConnectionsTaskExceptionOccured += WaitForPortConnectionsTaskExceptionEventHandler;
            tcpPeripheral.WaitForPortConnectionsTaskExceptionOccured += WaitForPortConnectionsTaskExceptionEventHandler;

            unidentifiedPorts = new List<Port>();

            clientsByType = new Dictionary<Client.TYPE, List<Client>>();
            for (int type = 0; type < (int)Client.TYPE.TYPECOUNT; type++)
            {
                clientsByType.Add((Client.TYPE)type, new List<Client>());
            }

            dataFlowRules = DataFlowRules.CreateNew(DataFlowRules.AllToAll);

        }

        static void PortRequestedEventHandler(object sender, PortRequestedEventArgs eventArgs)
        {
            try
            {
                Port port = eventArgs.port;
                port.Open();
                port.ClientConnectionRequested += ClientConnectionRequestedEventHandler;
                port.PortClosed += PortClosedEventHandler;
                port.ReadTaskExceptionOccured += PortExceptionOccuredEventHandler;
                port.WriteTaskExceptionOccured += PortExceptionOccuredEventHandler;
                port.WaitForConnectionRequestTaskExceptionOccured += PortExceptionOccuredEventHandler;

                unidentifiedPorts.Add(port);
            }
            catch(Exception e)
            {
                Util.DisplayExceptionInfo(e, "in PortRequestedHandler");
            }
        }

        static void ClientConnectionRequestedEventHandler(object sender, ClientConnectionRequestedEventArgs eventArgs)
        {
            Client newClient = eventArgs.client;
            AddDataFlow(newClient);
            clientsByType[newClient.type].Add(newClient);
            newClient.StartReading();

            Console.WriteLine("New Client: " + newClient.name);
        }

        static void PortClosedEventHandler(object sender, PortClosedEventArgs eventArgs)
        {
            Port port = (Port)sender;
            if (unidentifiedPorts.Contains(port))
            {
                unidentifiedPorts.Remove(port);
            }
            else
            {
                try
                {
                    Client client = clients.Single(c => c.port == port);
                    RemoveDataFlow(client);
                    clientsByType[client.type].Remove(client);
                }
                catch (Exception e)
                {
                    Util.DisplayExceptionInfo(e, "PortClosedEventHandler of " + port.ID);
                }
            }
           
        }

        static void PortExceptionOccuredEventHandler(object sender, ExceptionOccuredEventArgs eventArgs)
        {
            Util.DisplayExceptionInfo(eventArgs.exception, "in " + ((Peripheral)sender).ID);
        }

        static void WaitForPortConnectionsTaskExceptionEventHandler(object sender, ExceptionOccuredEventArgs eventArgs)
        {
            Util.DisplayExceptionInfo(eventArgs.exception, "in WaitForPortConnectionsTask of " + ((Peripheral)sender).ID); 
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

            public static Dictionary<Client.TYPE, List<Client.TYPE>> AllToAll = new Dictionary<Client.TYPE, List<Client.TYPE>>
            {
                [Client.TYPE.CONSOLE]       = new List<Client.TYPE> { Client.TYPE.CONSOLE, Client.TYPE.MOUSE, Client.TYPE.MATLAB, Client.TYPE.BOOTCOMMANDER },
                [Client.TYPE.MATLAB]        = new List<Client.TYPE> { Client.TYPE.CONSOLE, Client.TYPE.MOUSE, Client.TYPE.MATLAB, Client.TYPE.BOOTCOMMANDER },
                [Client.TYPE.BOOTCOMMANDER] = new List<Client.TYPE> { Client.TYPE.CONSOLE, Client.TYPE.MOUSE, Client.TYPE.MATLAB, Client.TYPE.BOOTCOMMANDER },
                [Client.TYPE.MOUSE]         = new List<Client.TYPE> { Client.TYPE.CONSOLE, Client.TYPE.MOUSE, Client.TYPE.MATLAB, Client.TYPE.BOOTCOMMANDER }
            };
        }
      

        static void Run()
        {
            Console.WriteLine("PortMediator v2");
            try
            {
                OpenAll();
            }
            catch(Exception e)
            {
                Util.DisplayExceptionInfo(e, "during opening peripherals");
            }
            //TCPTestClient tcpTestClient = new TCPTestClient();
            //tcpTestClient.StartClient(TCPPeripheral.localEndPoint);
            //tcpTestClient.StartReading();
            string input;
            do
            {
                input = Console.ReadLine();
                if(input == "restart")
                {
                    CloseAll();
                    OpenAll();
                }
                else if(input != "exit")
                {
                    //tcpTestClient.SendToHost(Encoding.ASCII.GetBytes(input));
                }
            } while (input != "exit");


            try
            {
                CloseAll();
            }
            catch (Exception e)
            {
                Util.DisplayExceptionInfo(e, "during closing peripherals");
            }
            Console.Read();


        }

        static void OpenAll()
        {
            serialPeripheral.Start();
            blePeripheral.Start();
            tcpPeripheral.Start();
        }

        static void CloseAll()
        {
            foreach (Client client in clients)
            {
                client.Close();
            }
            serialPeripheral.Stop();
            blePeripheral.Stop();
            tcpPeripheral.Stop();
        }

        static void AddDataFlow(Client newClient)
        {
            foreach(Client client in clients)
            {
                bool isSubscribingToNew = dataFlowRules[newClient.type].Contains(client.type);
                bool isNewSubscribing = dataFlowRules[client.type].Contains(newClient.type);

                if(isSubscribingToNew)
                {
                    newClient.DataReceived += client.SendData;
                }
                if (isNewSubscribing)
                {
                    client.DataReceived += newClient.SendData;
                }
            }
        }

        static void RemoveDataFlow(Client deletedClient)
        {
            foreach (Client client in clients)
            {
                try
                {
                    if (client != deletedClient)
                    {
                        bool isSubscribingToDeleted = dataFlowRules[deletedClient.type].Contains(client.type);
                        bool isDeletedSubscribing = dataFlowRules[client.type].Contains(deletedClient.type);

                        if (isSubscribingToDeleted)
                        {
                            deletedClient.DataReceived -= client.SendData;
                        }
                        if (isDeletedSubscribing)
                        {
                            client.DataReceived -= deletedClient.SendData;
                        }
                    }
                }
                catch(Exception e)
                {
                    Util.DisplayExceptionInfo(e, "in RemoveDataFlow of " + deletedClient.name + ", other client: " + client.name);
                }
               
            }
        }
    }
}
