using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using POSAgent.Utils;
using Newtonsoft.Json.Linq;
using System.IO;

namespace Tabsquare
{
    public class UtilsForSocket
    {
        //private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);



        #region Handle parse string have json format to array list

        public JArray ParseStringHaveFormatJsonToArray(string jsonString)
        {
            var arrs = new JArray();
            try
            {
                //Parse string have json format to array 
                arrs = JArray.Parse(jsonString);
            }
            catch (Exception ex)
            {
                var jObject = new JObject { new JProperty("ErrorMessage", ex.Message) };
                arrs.Add(jObject);
            }
            return arrs;
        }

        #endregion


        private static void DeplayServiceInTime()
        {
            int delayTime; // no delay
            try
            {
                delayTime = Int32.Parse(ConfigurationManager.AppSettings["DelayTime"].ToString(CultureInfo.InvariantCulture));
                if (delayTime < 0)
                {
                    delayTime = 0;
                }
            }
            catch (Exception)
            {
                delayTime = 0;
            }

            Thread.Sleep(delayTime);
        }

        public class Items
        {
            public string ItemKey { get; set; }
            public string ItemValue { get; set; }
        }

        public static byte[] ReadToEnd(System.IO.Stream stream)
        {
            long originalPosition = 0;

            if (stream.CanSeek)
            {
                originalPosition = stream.Position;
                stream.Position = 0;
            }

            try
            {
                byte[] readBuffer = new byte[4096];

                int totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = stream.Read(readBuffer, totalBytesRead, readBuffer.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;

                    if (totalBytesRead == readBuffer.Length)
                    {
                        int nextByte = stream.ReadByte();
                        if (nextByte != -1)
                        {
                            byte[] temp = new byte[readBuffer.Length * 2];
                            Buffer.BlockCopy(readBuffer, 0, temp, 0, readBuffer.Length);
                            Buffer.SetByte(temp, totalBytesRead, (byte)nextByte);
                            readBuffer = temp;
                            totalBytesRead++;
                        }
                    }
                }

                byte[] buffer = readBuffer;
                if (readBuffer.Length != totalBytesRead)
                {
                    buffer = new byte[totalBytesRead];
                    Buffer.BlockCopy(readBuffer, 0, buffer, 0, totalBytesRead);
                }
                return buffer;
            }
            finally
            {
                if (stream.CanSeek)
                {
                    stream.Position = originalPosition;
                }
            }
        }
    }
}
