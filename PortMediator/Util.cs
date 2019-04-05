using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PortMediator
{
    public static class Util
    {
        //public static bool IsEnumValueValid(byte value, byte maxEnumValue)
        //{
        //    return value < maxEnumValue;
        //}

        //public static class DictionaryUtils<TKey, TValue>
        //{
        //    public static void Init(ref Dictionary<TKey, TValue> dictionary)
        //    {
        //        for (int type = 0; type < (int)Client.TYPE.TYPECOUNT; type++)
        //        {
        //            //dictionary.Add(new TValue());
        //        }
        //    }
        //}

        public static byte[] ClipTrailingNullFromString(byte[] stringBytes)
        {
            byte[] dataBytes = null;
            if(stringBytes != null && stringBytes.Length > 0)
            {
                dataBytes = new byte[stringBytes.Length - 1];
                Array.Copy(stringBytes, dataBytes, dataBytes.Length);
            }
            return dataBytes;
        }
    }


}
