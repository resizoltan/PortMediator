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

        public BLEPort(ref BluetoothLEDevice device, GattCharacteristic characteristic, Action<Client> NewClientHandler) : base(NewClientHandler)
        {
            this.device = device;
            this.characteristic = characteristic;
        }

        public override string ID
        {
            get
            {
                return "BLE GATT Characteristic with UUID " + characteristic.Uuid.ToString();
            }
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
                    e.Source = "BLEPort.Open() of " + ID;
                    throw e;
                }
            }
        }

        public override void Close()
        {
            try
            {
                device.Dispose(); //what happens if there are other references to this device in other BLEPorts?
            }
            catch(Exception e)
            {
                e.Source = "BLEPeripheral.Close() of " + ID + " -> " + e.Source;
                throw e;
            }
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
            //data = Util.ClipTrailingNullFromString(data);
            if(data.Length == connectionRequestMessageLength)
            {
                OnConnectionRequest(this, data);
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
                e.Source = "BLEPort.StartReading() of " + ID + " -> " + e.Source;
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
                e.Source = "BLEPort.StopReading() of " + ID + " -> " + e.Source;
                throw e;
            }
        }

        public override void Write(byte[] data)
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
            }
            catch(Exception e)
            {
                e.Source = "BLEPort.SendData() of " + ID + " -> " + e.Source;
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

        public BLEPeripheral()
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

        public override void Start()
        {
            watcher.Start();
        }

        public override void Stop()
        {
            watcher.Stop();
        }

        public override string ID
        {
            get
            {
                return "BLEPeripheral";
            }
        }

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            BluetoothLEDevice device = null;
            try
            {
                watcher.Stop();

                device = await ConnectToDevice(eventArgs);

                GattDeviceService service = GetServiceData(device, wantedServiceUuidString);

                GattCharacteristic characteristic = GetCharacteristicData(service, wantedCharacteristicUuidString);

                BLEPort blePort = new BLEPort(device, characteristic);

                PortRequestedEventArgs portEventArgs = new PortRequestedEventArgs(blePort);
                OnPortRequested(portEventArgs);
            }
            catch (Exception e)
            {
                if (device != null)
                {
                    device.Dispose();
                }

                ExceptionOccuredEventArgs exceptionEventArgs = new ExceptionOccuredEventArgs(e);
                OnWaitForPortConnectionsExceptionOccured(exceptionEventArgs);
            }
            finally
            {
                watcher.Start();
            }
        }

        private async Task<BluetoothLEDevice> ConnectToDevice(BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            BluetoothLEDevice device = await BluetoothLEDevice.FromBluetoothAddressAsync(eventArgs.BluetoothAddress);

            if (device == null)
            {
                Exception e = new Exception("Connecting to bluetooth device failed");
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
                Exception e = new Exception("Retrieving bluetooth GATT service data failed");
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
                Exception e = new Exception("Retrieving bluetooth GATT characteristic data failed");
                throw e;
            }

            return characteristic;
        }

        //public override void Close()
        //{
        //    throw new NotImplementedException();
        //}
    }
}
