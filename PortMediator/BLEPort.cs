using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

using Windows.Storage.Streams;
using System.Threading.Tasks;
using System.Threading;

namespace PortMediator
{
    class BLEPort : Port
    {
        BluetoothLEAdvertisementWatcher bleWatcher_ = null;

        BluetoothLEDevice bleDevice_ = null;
        GattDeviceService gattService_ = null;
        GattCharacteristic gattCharacteristic_ = null;

        string bleDeviceLocalName_ = "JDY-10-V2.4";
        string gattServiceUuidString_ = "0000ffe0-0000-1000-8000-00805f9b34fb";
        string gattCharacteristicUuidString_ = "0000ffe1-0000-1000-8000-00805f9b34fb";

        DataWriter dataWriter_ = new DataWriter();
        DataReader dataReader_ = null;
        byte[] receivedBytes_ = null;

        private ManualResetEvent manualResetEvent_ = new ManualResetEvent(false);

        public override void SendData(byte[] data)
        {
            Action sendNextSlice = null; //action for the slice by slice transfer of data to the bluetooth module
            int bytesSent = 0;
            int maxSliceSize = 20; //empirically determined maximum (index out of array bonds exception in gattCharacteristic_.WriteValueAsync(...) for higher values)
            sendNextSlice = async delegate
            {
                int sliceSize = ((data.Length - bytesSent) > maxSliceSize) ?    //if   more bytes remain than the maximum ble msg size
                                maxSliceSize :                                  //     send maximum msg size amount of bytes
                                (data.Length - bytesSent);                      //else send remaining bytes
                byte[] slice = new byte[sliceSize];
                System.Buffer.BlockCopy(data, bytesSent, slice, 0, sliceSize);  //copy the respective bytes from data to a new array called slice
                dataWriter_.WriteBytes(slice);
                await gattCharacteristic_.WriteValueAsync(dataWriter_.DetachBuffer()); //send data slice to the bluetooth module
                bytesSent += sliceSize;
                if (bytesSent < data.Length)
                {
                    sendNextSlice(); //repeat process until whole data is transmitted
                }
            };
            sendNextSlice(); //transmit first slice
        }

        public async override Task<bool> OpenPort()
        {
            Func<bool> attemptToConnect = null;
            attemptToConnect =  delegate
            {
                StartWatching();
                manualResetEvent_.WaitOne();
                return true;
            };

            bool success = await Task<bool>.Run(attemptToConnect);
            return success;
           
        }

        public override void ClosePort()
        {
            bleDevice_.Dispose();
        }

        public async override Task<bool> StartReading()
        {
            bool success = false;

            Func<Task<bool>> attemptToSubscribe = async delegate
            {
                GattCommunicationStatus status = await gattCharacteristic_.WriteClientCharacteristicConfigurationDescriptorAsync(
                                GattClientCharacteristicConfigurationDescriptorValue.Notify);
                if (status == GattCommunicationStatus.Success)
                {
                    gattCharacteristic_.ValueChanged += BLEDataReceived;
                    return true;
                }
                else
                {
                    Console.WriteLine("Failed at subscribing to bluetooth GATT characteristic notifications, characteristic unreachable");
                    return false;
                }
            };
            if (bleDevice_.ConnectionStatus == BluetoothConnectionStatus.Connected &&
                gattCharacteristic_ != null)
            {
                success = await attemptToSubscribe();
            }
            else
            {
                Console.WriteLine("Failed at subscribing to bluetooth GATT characteristic notifications, not connected to device");
            }
            return success;
        }

        public BLEPort()
        {
            bleWatcher_ = new BluetoothLEAdvertisementWatcher();

            bleWatcher_.ScanningMode = BluetoothLEScanningMode.Active;

            bleWatcher_.SignalStrengthFilter.InRangeThresholdInDBm = -50;
            bleWatcher_.SignalStrengthFilter.OutOfRangeThresholdInDBm = -70;
            bleWatcher_.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromSeconds(5);
            bleWatcher_.SignalStrengthFilter.SamplingInterval = TimeSpan.FromSeconds(2);

            bleWatcher_.AdvertisementFilter.Advertisement.LocalName = bleDeviceLocalName_;
            
            bleWatcher_.Received += OnAdvertisementReceived;
        }

        private void StartWatching()
        {
            manualResetEvent_.Reset();
            bleWatcher_.Start();
        }

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher bleWatcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {

            bleWatcher_.Stop();

            bool successSofar = true;

            /*---------------------- Connect to Device --------------------------*/
            bleDevice_ = await BluetoothLEDevice.FromBluetoothAddressAsync(eventArgs.BluetoothAddress);

            if(bleDevice_ == null)
            {
                Console.WriteLine("Failed at connecting to bluetooth device");
                successSofar = false;
            }

            /*--------------------- Get GATT Service Data -----------------------*/
            if (successSofar == true)
            {
                try
                {
                    gattService_ = bleDevice_.GattServices.Single(service => service.Uuid == new Guid(gattServiceUuidString_));
                }
                catch
                {
                    Console.WriteLine("Failed at retrieving bluetooth GATT service data, more than one service found");
                    successSofar = false;
                }
            }

            if (successSofar == true)
            {
                if (gattService_ == null)
                {
                    Console.WriteLine("Failed at retrieving bluetooth GATT service data, service not found");
                    successSofar = false;
                }
            }

            /*------------------ Get GATT Characteristic Data --------------------*/
            if (successSofar == true)
            {
                try
                {
                    gattCharacteristic_ = gattService_.GetAllCharacteristics().Single(
                        characteristic => characteristic.Uuid == new Guid(gattCharacteristicUuidString_));
                }
                catch
                {
                    Console.WriteLine("Failed at retrieving bluetooth GATT characteristic, more than one characteristic found");
                    successSofar = false;
                }
            }

            if (successSofar == true)
            {
                if (gattCharacteristic_ == null)
                {
                    Console.WriteLine("Failed at retrieving bluetooth GATT characteristic data, characteristic not found");
                    successSofar = false;
                }
            }

            bleDevice_.ConnectionStatusChanged += (BluetoothLEDevice device, object o) =>
            {
                if(device.ConnectionStatus == BluetoothConnectionStatus.Connected)
                {
                    manualResetEvent_.Set();
                }
                else
                {
                    manualResetEvent_.Reset();
                }
            };


            
        }

        private void BLEDataReceived(GattCharacteristic gattCharacteristic, GattValueChangedEventArgs eventArgs)
        {
            dataReader_ = DataReader.FromBuffer(eventArgs.CharacteristicValue);
            receivedBytes_ = new byte[dataReader_.UnconsumedBufferLength];
            dataReader_.ReadBytes(receivedBytes_);
            OnDataReceived(receivedBytes_);
        }

    }
}
