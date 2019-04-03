using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace PortMediator
{
    class BLEPort : Port
    {
        GattCharacteristic characteristic = null;
        T

        public override string GetID()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> Open(Peripheral serialPeripheral)
        {
            throw new NotImplementedException();
        }

        public override void Close()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> WaitForConnectionRequest()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StartReading()
        {
            throw new NotImplementedException();
        }

        public override void StopReading(Client client)
        {
            throw new NotImplementedException();
        }

        public override void SendData(byte[] data)
        {
            throw new NotImplementedException();
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

        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            try
            {
                BluetoothLEDevice device = await ConnectToDevice(eventArgs);

                GattDeviceService service = GetServiceData(device, wantedServiceUuidString);

                GattCharacteristic characteristic = GetCharacteristicData(service, wantedCharacteristicUuidString);


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

        public override Task<bool> StartPeripheral()
        {
            throw new NotImplementedException();
        }

        public override Task<bool> StopPeripheral()
        {
            throw new NotImplementedException();
        }

        public override void ClosePeripheral()
        {
            throw new NotImplementedException();
        }
    }
}
