using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    public static class Communication
    {

        public enum COMMAND
        {
            UNDEFINED,
            SETDATA,
            GETDATA,
            GETALLDATA,
            SUSPEND,
            START,
            TEXT,
            COMMANDCOUNT
        }

        public enum VERBOSITY
        {
            NONE,
            DEBUG,
            INFO,
            ERROR,
            WARNING,
            VERBOSITYCOUNT
        }

        public static COMMAND GetCommand(Packet packet)
        {
            COMMAND command = COMMAND.UNDEFINED;
            byte[] data = packet.rawData;
            if (data.Length == 0)
            {
                Exception e = new Exception("Can't determine command, packet size is 0");
                e.Source = "Communication.GetCommand()";
                throw e;
            }
            else
            {
                byte commandByte = (byte)(data[0] - Encoding.ASCII.GetBytes("0")[0]);
                if (commandByte < (byte)COMMAND.COMMANDCOUNT)
                {
                    command = (COMMAND)data[0];
                }
                else
                {
                    Exception e = new Exception("Command " + commandByte + " doesn't exist");
                    e.Source = "Communication.GetCommand()";
                    throw e;
                }
            }
            return COMMAND.UNDEFINED;

        }

        public static VERBOSITY GetVerbosity(Packet packet)
        {
            VERBOSITY verbosity = VERBOSITY.NONE;
            byte[] data = packet.rawData;
            if (data.Length < 2)
            {
                Exception e = new Exception("Can't determine verbosity, packet size is less than 2");
                e.Source = "Communication.GetVerbosity()";
                throw e;
            }
            else
            {
                byte verbosityByte = (byte)(data[1] - Encoding.ASCII.GetBytes("0")[0]);
                if(verbosityByte < (byte)VERBOSITY.VERBOSITYCOUNT)
                {
                    verbosity = (VERBOSITY)verbosityByte;
                }
                else
                {
                    Exception e = new Exception("Verbosity " + verbosityByte + " doesn't exist");
                    e.Source = "Communication.GetVerbosity()";
                    throw e;
                }
            }
            return VERBOSITY.NONE;
        }

        //unsafe conversion to byte!
        public class Packet
        {
            private List<byte> data = new List<byte>();
            private int packetLength = -1;
            private bool isEmpty = true;

            public static Packet CreateFromXCP(byte[] xcpBytes)
            {
                Packet packet = new Packet();
                packet.xcp = xcpBytes;
                return packet;
            }

            public int PacketLength {
                get
                {
                    return packetLength;
                }
                set
                {
                    packetLength = value;
                }
            }

            public byte[] xcp {
                get
                {
                    byte[] xcpBytes = null;
                    if (data.Count != 0)
                    {
                        xcpBytes = new byte[data.Count + 1];
                        xcpBytes[0] = (byte)data.Count;
                        byte[] dataBytes = data.ToArray();
                        Array.Copy(dataBytes, 0, xcpBytes, 1, dataBytes.Length);
                    }
                    return xcpBytes;
                }
                set
                {
                    byte[] xcpBytes = value;

                    if(xcpBytes == null || xcpBytes.Length == 0)
                    {
                        data = new List<byte>();
                    }
                    else if(xcpBytes.Length == 1)
                    {
                        if(xcpBytes[0] != 0)
                        {
                            //might raise exception here
                        }
                        data = new List<byte>();
                    }
                    else
                    {
                        isEmpty = false;
                        packetLength = xcpBytes[0];
                        int dataLength = xcpBytes.Length;
                        byte[] dataBytes = new byte[dataLength];
                        Array.Copy(xcpBytes, 1, dataBytes, 0, dataLength);
                        data = dataBytes.ToList();
                    }
                }
            }

            public byte[] xcpBootCommander {
                get
                {
                    byte[] xcpBytes = null;
                    if (data.Count != 0)
                    {
                        xcpBytes = new byte[data.Count + 4];
                        xcpBytes[0] = 0; //first 4 bytes dummies, because bootcommander isn't using them
                        xcpBytes[1] = 0;
                        xcpBytes[2] = 0;
                        xcpBytes[3] = 0;
                        byte[] dataBytes = data.ToArray();
                        Array.Copy(dataBytes, 0, xcpBytes, 4, dataBytes.Length);
                    }
                    return xcpBytes;
                }
                set
                {
                    byte[] xcpBytes = value;
                    packetLength = -1;
                    isEmpty = false;

                    if (xcpBytes == null || xcpBytes.Length <= 4)
                    {
                        data = new List<byte>();
                    }
                    else
                    {
                        int dataLength = xcpBytes.Length - 4;
                        byte[] dataBytes = new byte[dataLength];
                        Array.Copy(xcpBytes, 4, dataBytes, 0, dataLength);
                        data = dataBytes.ToList();
                    }
                }
            }

            public byte[] rawData {
                get
                {
                    return data.ToArray();
                }
                set
                {
                    packetLength = -1;
                    isEmpty = false;

                    if (value != null)
                    {
                        data = value.ToList();
                    }
                    else
                    {
                        data = new List<byte>();
                    }
                }
            }

            public bool IsFinished()
            {
                bool answer = false;
                if(packetLength == -1 || packetLength == data.Count)
                {
                    answer = true;
                }
                else if(packetLength > data.Count)
                {
                    //might raise exception here
                    answer = true;
                }
                return answer;
            }

            public bool IsEmpty()
            {
                return isEmpty;
            }

            public void Clear()
            {
                data = new List<byte>();
                packetLength = -1;
                isEmpty = true;
            }

            public void Add(byte[] newDataBytes)
            {
                isEmpty = false;
                data.AddRange(newDataBytes.ToList());
                //handle here if new data.Count is larger than packetlength
            }
        }
    }
}
