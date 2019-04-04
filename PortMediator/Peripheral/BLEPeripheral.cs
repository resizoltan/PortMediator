using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;
using Windows.Storage.Streams;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace PortMediator
{
    class BLEPort : Port
    {
        GattCharacteristic characteristic = null;
        BluetoothLEDevice device = null;

        public BLEPort(BluetoothLEDevice device, GattCharacteristic characteristic, Action<Client> NewClientHandler) : base(NewClientHandler)
        {
            this.device = device;
            this.characteristic = characteristic;
        }

        public override string GetID()
        {
            return "BLE GATT Characteristic with UUID " + characteristic.Uuid.ToString();
        }

        public async override void Open()
        {
            if (device != null && 
                device.ConnectionStatus == BluetoothConnectionStatus.Connected && 
                characteristic != null)
            {
                GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if(status == GattCommunicationStatus.Success)
                {
                    StartWaitingForConnectionRequest();
                }
                else
                {
                    Exception e = new Exception("Subscribing to bluetooth GATT characteristic notifications failed, characteristic unreachable");
                    e.Source = "BLEPort.Open() of " + GetID();
                    throw e;
                }
            }
        }

        public override void Close()
        {
            device.Dispose(); //what happens if there are other references to this device in other BLEPorts?
        }

        public override void StartWaitingForConnectionRequest()
        {
            characteristic.ValueChanged += WaitingForConnectionCallback;
        }

        private void WaitingForConnectionCallback(GattCharacteristic gattCharacteristic, GattValueChangedEventArgs eventArgs)
        {
            DataReader dataReader = DataReader.FromBuffer(eventArgs.CharacteristicValue);
            byte[] data = new byte[dataReader.UnconsumedBufferLength];
            dataReader.ReadBytes(data);
            if(data.Length == connectionRequestMessageLength)
            {
                ConnectionRequested(this, data);
                characteristic.ValueChanged -= WaitingForConnectionCallback;
            }
        }

        private void BLEDataReceived(GattCharacteristic gattCharacteristic, GattValueChangedEventArgs eventArgs)
        {
            DataReader dataReader = DataReader.FromBuffer(eventArgs.CharacteristicValue);
            byte[] data = new byte[dataReader.UnconsumedBufferLength];
            dataReader.ReadBytes(data);
            OnDataReceived(data);
        }

        public override void StartReading()
        {
            try
            {
                characteristic.ValueChanged += BLEDataReceived;
            }
            catch(Exception e)
            {
                e.Source = "BLEPort.StartReading() of " + GetID() + " -> " + e.Source;
                throw e;
            }
        }

        public override void StopReading(Client client)
        {
            try
            {
                characteristic.ValueChanged -= BLEDataReceived;
            }
            catch (Exception e)
            {
                e.Source = "BLEPort.StopReading() of " + GetID() + " -> " + e.Source;
                throw e;
            }
        }

        public override Task SendData(byte[] data)
        {
            try
            {
                DataWriter dataWriter = new DataWriter();
                int bytesSent = 0;
                int maxSliceSize = 20; //empirically determined maximum (index out of array bonds exception in gattCharacteristic_.WriteValueAsync(...) for higher values)
                Task sendTask = Task.Factory.StartNew(async delegate
                {
                    while(bytesSent < data.Length)
                    {
                        int sliceSize = ((data.Length - bytesSent) > maxSliceSize) ?//if   more bytes remain than the maximum ble msg size
                                    maxSliceSize :                                  //     send maximum msg size amount of bytes
                                    (data.Length - bytesSent);                      //else send remaining bytes
                        byte[] slice = new byte[sliceSize];
                        System.Buffer.BlockCopy(data, bytesSent, slice, 0, sliceSize);  //copy the respective bytes from data to a new array called slice
                        dataWriter.WriteBytes(slice);
                        await characteristic.WriteValueAsync(dataWriter.DetachBuffer()); //send data slice to the bluetooth module
                        bytesSent += sliceSize;
                    }
                });
                return sendTask;
            }
            catch(Exception e)
            {
                e.Source = "BLEPort.SendData() of " + GetID() + " -> " + e.Source;
                throw e;
            }
        }
    
    }


    class BLEPeripheral : Peripheral
    {
        BluetoothLEAdvertisementWatcher watcher = null;
        readonly string wantedDeviceLocalName = "JDY-10-V2.4";
        string wantedServiceUuidString = "0000ffe0-0000-1000-8000-00805f9b34fb";
        string wantedCharacteristicUuidString = "0000ffe1-0000-1000-8000-00805f9b34fb";

        public BLEPeripheral(Action<Client> NewClientHandler) : base(NewClientHandler)
        {
            watcher = new BluetoothLEAdvertisementWatcher();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;

            watcher.SignalStrengthFilter.InRangeThresholdInDBm = -50;
            watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -70;
            watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromSeconds(5);
            watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromSeconds(1);

            watcher.AdvertisementFilter.Advertisement.LocalName = wantedDeviceLocalName;

            watcher.Received += OnAdvertisementReceived;
        }

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            try
            {
                BluetoothLEDevice device = await ConnectToDevice(eventArgs);

                GattDeviceService service = GetServiceData(device, wantedServiceUuidString);

                GattCharacteristic characteristic = GetCharacteristicData(service, wantedCharacteristicUuidString);

                BLEPort blePort = new BLEPort(device, characteristic, NewClientHandler);

                ports.Add(blePort);

                blePort.Open();
            }
            catch(Exception e)
            {
                Console.WriteLine("Error occured in BLEPeripheral.OnAdvertisementReceived() during connecting to device " + eventArgs.Advertisement.LocalName);
                Console.WriteLine("\tError source: " + e.Source);
                Console.WriteLine("\tError message: " + e.Message);
            }
        }

        private async Task<BluetoothLEDevice> ConnectToDevice(BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(eventArgs.BluetoothAddress);

            if (device == null)
            {
                Exception e = new Exception("Connecting to bluetooth device failed");
                e.Source = "ConnectToDevice()";
                throw e;
            }

            return device;
        }

        private GattDeviceService GetServiceData(BluetoothLEDevice device, string serviceUuidString)
        {
            GattDeviceService service = null;
            try
            {
                service = device.GattServices.Single(s => s.Uuid == new Guid(wantedServiceUuidString));
            }
            catch
            {
                Exception e = new Exception("Retrieving bluetooth GATT service data failed, more than one service found");
                e.Source = "GetServiceData()";
                throw e;
            }

            if (service == null)
            {
                Exception e = new Exception("Retrieving bluetooth GATT service data failed, service not found");
                e.Source = "GetServiceData()";
                throw e;
            }

            return service;
        }

        private GattCharacteristic GetCharacteristicData(GattDeviceService service, string characteristicUuidString)
        {
            GattCharacteristic characteristic = null;
            try
            {
                characteristic = service.GetAllCharacteristics().Single(
                    c => c.Uuid == new Guid(wantedCharacteristicUuidString));
            }
            catch
            {
                Exception e = new Exception("Retrieving bluetooth GATT characteristic data failed, more than one characteristic found");
                e.Source = "GetCharacteristicData()";
                throw e;
            }

            if (characteristic == null)
            {
                Exception e = new Exception("Retrieving bluetooth GATT characteristic data failed, characteristic not found");
                e.Source = "GetCharacteristicData()";
                throw e;
            }
            return characteristic;
        }

        public override void Start()
        {
            watcher.Start();
        }

        public override void Stop()
        {
            base.Stop();
            watcher.Stop();
        }


        //public override void Close()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
