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

        public BLEPort(BluetoothLEDevice device, GattCharacteristic characteristic)
        {
            this.device = device;
            this.characteristic = characteristic;
            this.id = "BLE GATT Characteristic with UUID " + characteristic.Uuid.ToString();
        }

        public override void Open()
        {
            if(device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                throw new PortClosedException();
            }
            StartWaitingForConnectionRequest();
        }

        public override void Close()
        {
            if (device.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                throw new PortClosedException();
            }
            device.Dispose(); 

        }

        public override void StartWaitingForConnectionRequest()
        {
            characteristic.ValueChanged += WaitingForConnectionCallback;
        }

        private void WaitingForConnectionCallback(GattCharacteristic gattCharacteristic, GattValueChangedEventArgs eventArgs)
        {
            try
            {
                DataReader dataReader = DataReader.FromBuffer(eventArgs.CharacteristicValue);
                byte[] data = new byte[dataReader.UnconsumedBufferLength];
                dataReader.ReadBytes(data);
                if (data.Length == connectionRequestMessageLength)
                {
                    ConnectionRequestedEventArgs conReqEventArgs = new ConnectionRequestedEventArgs(data);
                    OnConnectionRequest(conReqEventArgs);
                    characteristic.ValueChanged -= WaitingForConnectionCallback;
                }
            }
            catch(Exception e)
            {
                ExceptionOccuredEventArgs exceptionOccuredEventArgs = new ExceptionOccuredEventArgs(e);
                OnWaitForConnectionRequestExceptionOccured(exceptionOccuredEventArgs);
            }
        }

        private void BLEDataReceived(GattCharacteristic gattCharacteristic, GattValueChangedEventArgs eventArgs)
        {
            try
            {
                DataReader dataReader = DataReader.FromBuffer(eventArgs.CharacteristicValue);
                byte[] data = new byte[dataReader.UnconsumedBufferLength];
                dataReader.ReadBytes(data);
                BytesReceivedEventArgs bytesReceivedEventArgs = new BytesReceivedEventArgs(data);
                OnDataReceived(bytesReceivedEventArgs);
            }
            catch (Exception e)
            {
                ExceptionOccuredEventArgs exceptionOccuredEventArgs = new ExceptionOccuredEventArgs(e);
                OnWaitForConnectionRequestExceptionOccured(exceptionOccuredEventArgs);
            }
        }

        public override void StartReading()
        {
            characteristic.ValueChanged += BLEDataReceived;
        }

        public override void StopReading(Client client)
        {
            characteristic.ValueChanged -= BLEDataReceived;
        }

        public override void Write(byte[] data)
        {
            writeTask = StartWrite(data);
        }

        private async Task StartWrite(byte[] data)
        {
            try
            {
                DataWriter dataWriter = new DataWriter();
                int bytesSent = 0;
                int maxSliceSize = 20; //empirically determined maximum (index out of array bonds exception in gattCharacteristic_.WriteValueAsync(...) for higher values)

                while (bytesSent < data.Length)
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
            }
            catch (Exception e)
            {
                ExceptionOccuredEventArgs exceptionOccuredEventArgs = new ExceptionOccuredEventArgs(e);
                OnWaitForConnectionRequestExceptionOccured(exceptionOccuredEventArgs);
            }
        }
    }


    class BLEPeripheral : Peripheral
    {


        BluetoothLEAdvertisementWatcher watcher = null;
        ManualResetEvent connectedToDeviceManualResetEvent = new ManualResetEvent(false);
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

                device.ConnectionStatusChanged += async (BluetoothLEDevice dev, object o) =>
                {
                    try
                    {
                        if (dev.ConnectionStatus == BluetoothConnectionStatus.Connected)
                        {
                            GattCommunicationStatus status = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                                                            GattClientCharacteristicConfigurationDescriptorValue.Notify);
                            PortRequestedEventArgs portEventArgs = new PortRequestedEventArgs(blePort);
                            OnPortRequested(portEventArgs);
                        }
                    }
                    catch (Exception e)
                    {
                        if (dev != null)
                        {
                            dev.Dispose();
                        }

                        ExceptionOccuredEventArgs exceptionEventArgs = new ExceptionOccuredEventArgs(e);
                        OnWaitForPortConnectionsExceptionOccured(exceptionEventArgs);
                    }
                    
                };

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
