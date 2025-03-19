using POSAgent.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Configuration;
using System.Globalization;
using Newtonsoft.Json;
using System.Collections;
using PaymentManager.DataContracts;
using Newtonsoft.Json.Linq;
using System.Web.Script.Serialization;
using Tabsquare.Payment;
using System.Text.RegularExpressions;
using Tabsquare;
using System.Net.NetworkInformation;

namespace PaymentManager
{
    public partial class MainFlow : ServiceBase
    {
        private readonly UtilsForSocket _mainFunction = new UtilsForSocket();

        public static bool IS_DEBUG = ConfigurationManager.AppSettings["DEBUG"] == "2606";

        static private bool _isServerRunning;

        public static string  ipAddress = ConfigurationSettings.AppSettings["LISTENING_IP"];
        public static string port = ConfigurationSettings.AppSettings["LISTENING_PORT"];

        
        public MainFlow()
        {
            try
            {
                Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);

                _isServerRunning = true;
                
                SocketManager.InitServerSocket();
                
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        private void AnalyzePayload()
        {


            //string payload = "#0161M000P00000000000000000100000000250720181401000 00                                                                  00              AP                    000";
            //string reg = "(.1)(.4)(.1)(.1)(.2)(.1)(.1)(.1)(.9)(.9)(.6)(.16)(.1)(.1)(.1)(.20)(.4)(.40)(.1)(.2)(.12)(.3)(.1)(.6)(.6)(.8)(.3)";

            string payload = "ABC";
            string reg = ".{1}.{1}.{1}";

            Match m = Regex.Match(payload, reg);

            Console.WriteLine("\r\n Length: " + payload.Length);
            Console.WriteLine(m.Success);
            Console.WriteLine("\r\n Number of groups: " + m.Groups.Count);
            Console.WriteLine("\r\n Number of groups: " + m.Groups[0]);

            //Console.WriteLine("\r\n Number of captures: " + m.Groups[0].Captures[0].);


        }

        static private byte CalCheckSum(byte[] _PacketData, int PacketLength)
        {
            Byte _CheckSumByte = 0x00;
            for (int i = 1; i < PacketLength; i++)
                _CheckSumByte ^= _PacketData[i];
            return _CheckSumByte;
        }

        private static string ReformatPrice(decimal price)
        {
            return price.ToString().Replace(".", "");
        }

        private static void TestSendHexa1()
        {


            /// Convert Char to Hexa
            //char ch1 = '9';            
            //byte b = 0x7;
            //Console.Write( "[" + (char)b + "]");

            byte[] byteList = new byte[] {
                                           0x02,
                                           0x00, 0x35,
                                           0x36, 0x30,
                                           0x30, 0x30, 0x30, 0x30,
                                           0x30, 0x30, 0x30, 0x30,

                                           0x31,
                                           0x30,
                                           0x32, 0x30,
                                           0x30, 0x30,
                                           0x30,
                                           0x1C,

                                           0x34, 0x30,
                                           0x00, 0x12,
                                           0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x30, 0x31, 0x30, 0x30,
                                           0x1C,
                                           0x03, 0x14
            };

            decimal total = 9999.99M;

            string str = ReformatPrice(total);

            int i = 36;
            foreach (char ch in str.Reverse())
            {
                byteList[i] = Convert.ToByte(ch);
                i--;
            }

            byte lrc = CalCheckSum(byteList, byteList.Count() - 1);
            byteList[byteList.Length - 1] = lrc;

            Console.WriteLine(lrc.ToString("0xx2"));

            ProcessByteResponse(byteList);

            /*
            byte a1 = 0x1c, b1 = 0x03, c1 = 0x3f;
            string breakingPoint = ((char)a1 + (char)b1 + (char)c1).ToString();
            string breakingPoint1 = ((char)a1).ToString() + ((char)b1).ToString() + ((char)c1).ToString();
            Logger.Log(breakingPoint);
            Logger.Log(breakingPoint1);
            */


            string ip = "192.168.1.62";
            int port = 2001;

            TcpClient posClient = new TcpClient(ip, port);
            BinaryWriter binaryWriter = null;
            BinaryReader binaryReader = null;
            Stream terminalStream = null;
            StreamReader terminalStreamReader = null;
            StreamWriter terminalStreamWriter = null;

            try
            {
                if (posClient.Connected)
                {
                    terminalStream = posClient.GetStream();

                    if (terminalStream != null)
                    {

                        posClient.ReceiveTimeout = 60000;
                        posClient.SendBufferSize = 100000;
                        posClient.ReceiveBufferSize = 100000;

                        terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                        terminalStream.WriteTimeout = 30000;

                        terminalStreamReader = new StreamReader(terminalStream);
                        //terminalStreamWriter = new StreamWriter(terminalStream);
                        //terminalStreamWriter.AutoFlush = true;                    

                        binaryWriter = new BinaryWriter(terminalStream);
                        binaryWriter.Write(byteList);
                        Logger.Log("Sent Successfully!");

                        Thread.Sleep(1000);

                        binaryReader = new BinaryReader(terminalStream);
                        string response = ReceiveEpointSocketMessage(DateTime.Now, terminalStream, posClient.Client);

                        //Logger.Log("Response: " + response);

                        //string response = ReceiveEpointSocketMessage(DateTime.Now, posClient.Client);


                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);


            }
            finally
            {
                try
                {
                    if (binaryWriter != null)
                        binaryWriter.Close();

                    if (binaryReader != null)
                        binaryReader.Close();

                    if (posClient != null && posClient.Connected)
                        posClient.Close();
                }
                catch (Exception ex1)
                {
                    Logger.Log(ex1);
                }

            }
            Logger.Log("FINISHED EVERYTHING!!!!");

            //List<byte> payLoad = byteList.ToList();
            //Logger.Log("Length: " + byteList.Count() );            
        }

        public static string ReceiveEpointSocketMessage(DateTime sendingTime, Stream terminalStream, Socket socket)     //encodeType: 1: ASCII; 2: UTF8; 3: Unicode
        {
            var intSocketLength = 100000;

            int encodeType = 1;

            socket.ReceiveBufferSize = 100000;
            socket.SendBufferSize = 100000;

            Logger.Log("Processing...");

            Encoding ascii = Encoding.ASCII;
            Encoding utf8 = Encoding.UTF8;
            Encoding unicode = Encoding.Unicode;

            bool isReceivedACK = false;

            //string breakingPoint = ((char)0x1c).ToString() + ((char)0x03).ToString() + ((char)0x3f).ToString();
            string breakingPoint = ((char)0x1c).ToString() + ((char)0x03).ToString();

            string tranId = string.Empty;
            string pieceOfData = string.Empty;
            string finalResposne = string.Empty;

            try
            {
                while (true)
                {
                    var bytes = new byte[intSocketLength];

                    // set the timeout is 10 ms because the socket on localhost
                    if (socket.Poll(PublicConstant.SocketTimeOut, SelectMode.SelectRead))// microseconds
                    {
                        var bytesRec = socket.Receive(bytes);
                        switch (encodeType)
                        {
                            //ASCII
                            case 1:
                                //dataResponse += ascii.GetString(bytes, 0, bytesRec);
                                pieceOfData = ascii.GetString(bytes, 0, bytesRec);
                                break;
                            //UTF8
                            case 2:
                                //dataResponse += utf8.GetString(bytes, 0, bytesRec);
                                Decoder uniDecoder = Encoding.UTF8.GetDecoder();

                                int charCount = uniDecoder.GetCharCount(bytes, 0, bytesRec);
                                var chars = new Char[charCount];
                                int charsDecodedCount = uniDecoder.GetChars(bytes, 0, bytesRec, chars, 0);

                                foreach (Char c in chars)
                                {
                                    pieceOfData += c;
                                }
                                break;
                            //unicode
                            case 3:
                                pieceOfData += unicode.GetString(bytes, 0, bytesRec);
                                break;
                        }

                        //var bytesRec = socket.Receive(bytes);

                        //if (bytesRec > 0)
                        //  ProcessByteResponse(bytes);
                    }
                    else
                        pieceOfData = string.Empty;

                    if (pieceOfData != null && pieceOfData.Length > 0)
                    {
                        ProcessStringResponse(pieceOfData);
                        finalResposne += pieceOfData;

                        if (pieceOfData.Contains(breakingPoint))
                        {
                            Logger.Log("Breaking..");
                            break;
                        }
                    }

                    if (pieceOfData.Length == 0)
                    {
                        Logger.Log("...");
                    }



                    /*
                    if (dataResponse.Length == 0)
                    {
                        Logger.Log("...");                                                                                        
                    }
                    else
                    {
                        Console.WriteLine("Length of the data:" + dataResponse.Length);
                        Console.WriteLine("The data:" + dataResponse);

                        if (dataResponse.Contains((char)0x06))
                        {                                                        
                            var currentTimestamp = DateTime.Now;
                            var diffInSeconds = (currentTimestamp - sendingTime).TotalSeconds;
                            Console.WriteLine("Received ACK in " + diffInSeconds + " seconds");
                            Console.WriteLine("Reset. Listening to response..." + diffInSeconds + " seconds");

                            dataResponse = String.Empty;

                        }
                        /// IF HAVE :::: Start of Text and End of Text
                        else if (dataResponse.Contains((char)0x01) && dataResponse.Contains((char)0x03) && dataResponse.Length > 9)
                        {
                            ProcessResponse(dataResponse);

                            Console.WriteLine("[" + dataResponse + "]");
                            Console.WriteLine("Breaking out...");                            
                            break;
                        }                                                              
                    }*/

                    Thread.Sleep(300);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            ProcessResponse(finalResposne);

            return pieceOfData;
        }

        private static void ProcessStringResponse(string dataResponse)
        {
            List<char> chList = dataResponse.ToList();

            Logger.Log("\r\n");
            Logger.Log("Length of the data:" + chList.Count());

            string byteString = string.Empty;
            foreach (char ch in chList)
            {
                byteString += Convert.ToByte(ch).ToString("x2") + " ";
            }
            Logger.Log("Data Response in Bytes:" + byteString);
            Logger.Log("Data Response in String :" + dataResponse);
            Logger.Log("\r\n");
        }

        private static void ProcessByteResponse(byte[] bytes)
        {
            Console.WriteLine("123 Number of bytes:" + bytes.Count());
            Console.WriteLine("\r\n");
            foreach (byte b in bytes)
            {
                Logger.Log(b.ToString("x2") + " ");
            }
            Console.WriteLine("\r\n");
        }

        private static bool ProcessResponse(string dataResponse)
        {
            Logger.Log("Processing the final String: " + dataResponse);

            if (dataResponse.Contains("6000000000112000"))
            {
                Logger.Log("The transaction has been processed successfully!!!!");
                return true;
            }
            else
            {
                Logger.Log("The transaction is not successful!");
                return false;
            }
        }

        private static void ConvertFromStringToByte(string dataResponse)
        {
            List<char> chList = dataResponse.ToList();

            Console.WriteLine();
            foreach (char ch in chList)
            {
                Console.Write(Convert.ToByte(ch).ToString("x2") + " ");
            }
            Console.WriteLine();
        }


        private static void TestSendHexa()
        {
            try
            {
                Stream serviceStream, ePointPOSStream;

                TcpClient posClient = null;

                try
                {
                    // call to pos
                    string ePointIP = "192.168.1.62";
                    int ePointPort = 2001;

                    posClient = new TcpClient(ePointIP, ePointPort);

                    if (posClient.Connected)
                    {
                        posClient.SendTimeout = 60000;
                        posClient.ReceiveTimeout = 60000;

                        /*
                        ePointPOSStream = posClient.GetStream();
                        ePointPOSStream.ReadTimeout = 60000; // set read timeout and write timeout to be 60s
                        ePointPOSStream.WriteTimeout = 60000;

                        StreamReader ePointStreamReader = new StreamReader(ePointPOSStream);
                        StreamWriter ePointStreamWriter = new StreamWriter(ePointPOSStream);
                        ePointStreamWriter.AutoFlush = true;

                        Thread.Sleep(200); // sleep 300 ms as EPoint suggestion

                        var content = "";

                        var messageSend = content;*/


                        /*
                        byte[] message = new byte[] { 02, 00, 43, 50, 30, 56, 31, 38, 31, 32, 33, 34, 35, 36, 37, 38,
                                                      39, 30, 41, 42, 43, 44, 45, 46, 47, 20, 20, 20, 30, 30, 30, 30,
                                                      30, 30, 30, 30, 34, 35, 30, 30, 30, 30, 30, 30, 30, 30, 03, 10
                        };                                                        
                        int responseCode = posClient.Client.Send(message);
                        Console.WriteLine(responseCode);
                         */

                        string message = "001930303030303030303233343738303031301C27";

                        NetworkStream stream = posClient.GetStream();
                        Byte[] data = System.Text.Encoding.ASCII.GetBytes(message);

                        // Send the message to the connected TcpServer. 
                        stream.Write(data, 0, data.Length);

                        Console.WriteLine("Sent: {0}", message);

                        // Receive the TcpServer.response.                            

                        // Buffer to store the response bytes.
                        data = new Byte[256];

                        // String to store the response ASCII representation.
                        String responseData = String.Empty;

                        // Read the first batch of the TcpServer response bytes.
                        Int32 bytes = stream.Read(data, 0, data.Length);
                        responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytes);
                        Console.WriteLine("Received: {0}", responseData);

                        // Close everything.
                        stream.Close();
                    }
                }
                catch (ArgumentNullException e)
                {
                    Logger.Log(e);
                }
                catch (SocketException e)
                {
                    Logger.Log(e);
                }
                catch (Exception ex)
                {
                    try
                    {
                        Logger.Log(ex);
                        //Logger.Log.Error("Exception " + ex + ex.StackTrace.ToString());
                    }
                    catch (Exception)
                    { }
                }
                finally
                {
                    try
                    {
                        if (posClient != null) ;
                        posClient.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }
                }
            }
            catch (Exception ex2)
            {
                Logger.Log(ex2);
            }
        }



        protected override void OnStart(string[] args)
        {
            Logger.Log("Agent socket is starting...");
            try
            {
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(POSAgent_UnhandledException);

                _isServerRunning = true;

                Logger.Log("Started");

                SocketManager.InitServerSocket();
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static string HexStringToString(string HexString)
        {
            string stringValue = "";
            for (int i = 0; i < HexString.Length / 2; i++)
            {
                string hexChar = HexString.Substring(i * 2, 2);
                int hexValue = Convert.ToInt32(hexChar, 16);
                stringValue += Char.ConvertFromUtf32(hexValue);
            }
            return stringValue;
        }

        public static string FromHexString(string hexString)
        {
            var bytes = new byte[hexString.Length / 2];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            return Encoding.Unicode.GetString(bytes); // returns: "Hello world" for "48656C6C6F20776F726C64"
        }

        public Double getTimeSpan()
        {
            TimeSpan ts = DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0);
            return ts.TotalMilliseconds;
        }

        protected override void OnStop()
        {
            //LogFiles.WriteLog("Agent socket stop");
            Logger.Log("Stopping agent socket...");

            _isServerRunning = false;
        }

        protected override void OnShutdown()
        {
            //LogFiles.WriteLog("Agent socket shut down");
            Logger.Log("Shutting down agent socket...");
            _isServerRunning = false;
            base.OnShutdown();
        }

        void POSAgent_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = (Exception)e.ExceptionObject;
            Logger.Log(ex);

            try
            {
                var service = new ServiceController("POSAgent");

                if ((service.Status.Equals(ServiceControllerStatus.Stopped)) || (service.Status.Equals(ServiceControllerStatus.StopPending)))
                {
                    Logger.Log("start Agent service");
                    service.Start();
                }
                else
                {
                    Logger.Log("restart Agent service");
                    service.Stop();
                    service.WaitForStatus(ServiceControllerStatus.Stopped);
                    service.Start();
                    service.WaitForStatus(ServiceControllerStatus.Running);
                }
            }
            catch (Exception exc)
            {
                try
                {
                    Logger.Log(exc);
                    Logger.Log("Exception Cannot restart service");
                }
                catch (Exception)
                { }
            }
        }
    }
}
