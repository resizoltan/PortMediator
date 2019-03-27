using System;
using System.Collections.Generic;
using System.Text;


using System.IO.Ports;
using System.IO;
using System.Threading.Tasks;

namespace PortMediator
{
    class SERIALPort : Port
    {
        SerialPort serialPort_ = new SerialPort();

        public override void SendData(byte[] data)
        {
            serialPort_.Write(data, 0, data.Length);
        }

        public SERIALPort(string portName, int baudRate)
        {
            serialPort_.PortName = portName;
            serialPort_.BaudRate = baudRate;
            serialPort_.Parity = Parity.None;
            serialPort_.ReadBufferSize = 100;

        }

        public async override Task<bool> OpenPort()
        {
            bool isSuccessful = await Task<bool>.Run( () =>
            {
                serialPort_.Open();
                return serialPort_.IsOpen;
            });
            if (isSuccessful)
            {
                serialPort_.DiscardInBuffer();
                serialPort_.DiscardOutBuffer();
            }
            return isSuccessful;
        }

        public override void ClosePort()
        {
            serialPort_.Close();
        }

        public async override Task<bool> StartReading()
        {
            byte[] buffer = new byte[serialPort_.ReadBufferSize];
            Action readNextByte = null;
            bool success = true;
            readNextByte = async delegate
            {
                if (serialPort_.IsOpen)
                {
                    int dataLength = await serialPort_.BaseStream.ReadAsync(buffer, 0, 1);
                    byte[] data = new byte[dataLength];
                    Array.Copy(buffer, data, dataLength);
                    if (dataLength != 1)
                    {
                        Console.WriteLine("WARNING: serial port read data size is " + dataLength);
                    }
                    else
                    {
                        //Console.WriteLine("SERIALDataReceived: " + data[0].ToString("X"));
                        OnDataReceived(data);
                    }
                    readNextByte();
                }
                else
                {
                    success = false;
                }
            };
            readNextByte();
            return success;
        }
    }
}
