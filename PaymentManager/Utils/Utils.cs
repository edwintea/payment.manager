using System;
using System.Linq;
using System.Text;

namespace PaymentManager.Utils
{
    class Utils
    {
        public static string FormatUTF8(string itemName)
        {
            Decoder uniDecoder = Encoding.UTF8.GetDecoder();

            Byte[] encodedBytes = Encoding.UTF8.GetBytes(itemName);
            int charCount = uniDecoder.GetCharCount(encodedBytes, 0, encodedBytes.Count());
            var chars = new Char[charCount];
            uniDecoder.GetChars(encodedBytes, 0, encodedBytes.Count(), chars, 0);
            string dataResponse = string.Empty;
            foreach (Char c in chars)
            {
                dataResponse += c;
            }

            return dataResponse;
        }

    }
}
