using Newtonsoft.Json;
using PaymentManager;
using PaymentManager.DataContracts;
using PaymentManager.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;


namespace Tabsquare.Payment
{

    public class OCBCImplementation : PaymentInterface
    {

        public enum LogType { Info, Error, Warning };
        public enum TransactionStatus { Paid, Failed, Cancelled };

        protected JavaScriptSerializer _javaScriptSerializer;

        public static TransactionResponse transactionResponse;
        public static string ResponseText;
        public static Boolean isSucccessfull = true;
        private static DBConnect connect = new DBConnect();
        private static string payload = string.Empty;

        public static String IP_TERMINAL = ConfigurationSettings.AppSettings["IP_TERMINAL"];
        public static int PORT_TERMINAL = Convert.ToInt32(ConfigurationSettings.AppSettings["PORT_TERMINAL"]);
        public static int ECR_TYPE = Convert.ToInt32(ConfigurationSettings.AppSettings["ECR_TYPE"]);
        public static String uniqueId = String.Empty;

        public JavaScriptSerializer JavaScriptSerializer
        {
            get
            {
                if (_javaScriptSerializer == null)
                {
                    _javaScriptSerializer = new JavaScriptSerializer();
                    _javaScriptSerializer.RegisterConverters(new[] { new POSAgent.Utils.DynamicJsonConverter() });
                }

                return _javaScriptSerializer;
            }
        }

        public string MERCHANT_ID
        {
            get
            {
                try
                {
                    return ConfigurationManager.AppSettings["MERCHANT_ID"].ToString();
                }
                catch (Exception)
                {
                    throw new Exception(String.Format(ErrorEnum.TABSQUARE_CONFIG_NOT_FOUND, "MERCHANT_ID"));
                }
            }
        }


        public string ExperimentSendHexa()
        {
            try
            {
                return string.Empty;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public Result DoLogOn(string merchant, string terminal)
        {
            int bufferSize = 100000;
            //FUNCTION CODE 80
            //sample based on doc
            //00-19-30-30-30-30-30-30-30-30-32-31-39-36-38-30-30-31-30-1C-29
            byte[] byteList = new byte[] { };

            try
            {
                if (ECR_TYPE == 1)
                {
                    byteList = new byte[] {
                        0x00,
                        0x19,

                        0x30,0x30,//ECN TO 0
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,

                        0x38,0x30,//COMMAND TYPE

                        0x30,0x30,//VERSION CODE TO 0
                        0x30,
                        0x1C,
                        0x2B
                    };
                }
                else if (ECR_TYPE == 2)
                {
                    byteList = new byte[] {
                        0x00,
                        0x19,

                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x32,0x30,
                        0x39,0x30,

                        0x38,0x30,

                        0x30,0x31,
                        0x30,
                        0x1C,
                        0x2B
                    };

                }

                //end set bytelist

                //collect LRC
                byte[] LRC = new byte[18];
                int y = 0;
                for (var g = 2; g < (byteList.Length - 1); g++)
                {
                    LRC[y] = Convert.ToByte(byteList[g]);
                    y++;
                }


                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {
                    payLoadInStr.Append(b.ToString("x2") + " ");
                }

                //Logger.Log("Item of LRC :\n" + payLoadInStr.ToString());

                byte lrc = CalCheckSum(LRC, LRC.Count());

                //set LRC to byteList
                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

                //prepare send to terminal
                TcpClient posClient = new TcpClient(IP_TERMINAL, PORT_TERMINAL);
                BinaryWriter binaryWriter = null;
                BinaryReader binaryReader = null;
                Stream terminalStream = null;


                try
                {
                    if (posClient.Connected)
                    {
                        terminalStream = posClient.GetStream();


                        if (terminalStream != null)
                        {
                            posClient.ReceiveTimeout = 60000;
                            posClient.SendBufferSize = bufferSize;
                            posClient.ReceiveBufferSize = bufferSize;

                            terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                            terminalStream.WriteTimeout = 30000;

                            binaryWriter = new BinaryWriter(terminalStream);
                            binaryWriter.Write(byteList);

                            Logger.Log("Sent. Waiting for the response...");

                            try
                            {
                                binaryReader = new BinaryReader(terminalStream);

                                binaryReader.Read(byteList, 0, byteList.Length);

                                ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, "LOGON");
                                return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };


                            }
                            catch (Exception t)
                            {
                                int count = 0;
                                while (true)
                                {

                                    binaryReader = new BinaryReader(terminalStream);

                                    binaryReader.Read(byteList, 0, byteList.Length);

                                    string cek = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, "logon");

                                    if (!String.IsNullOrEmpty(cek))
                                    {
                                        return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };

                                    }

                                    if (count > 40)
                                    {
                                        Console.WriteLine("Error Reader : " + t.Message.ToString());

                                        try
                                        {
                                            if (binaryWriter != null)
                                            {
                                                binaryWriter.Close();
                                                Console.WriteLine("Binary Writer clear!");
                                            }
                                            else
                                            {
                                                binaryWriter.Close();
                                                Console.WriteLine("Binary Writer clear!");
                                            }


                                        }
                                        catch (Exception ex1)
                                        {
                                            Logger.Log("" + ex1);
                                        }

                                        try
                                        {
                                            if (binaryReader != null)
                                            {
                                                binaryReader.Close();
                                                Console.WriteLine("Binary Reader clear!");
                                            }
                                            else
                                            {
                                                binaryReader.Close();
                                                Console.WriteLine("Binary Reader clear!");
                                            }

                                        }
                                        catch (Exception ex1)
                                        {
                                            Logger.Log(ex1);
                                        }

                                        try
                                        {

                                            posClient.Close();
                                            Console.WriteLine("Machine clear!");

                                        }
                                        catch (Exception ex1)
                                        {
                                            Logger.Log(ex1);
                                        }


                                        //start post to response
                                        transactionResponse = new TransactionResponse();
                                        transactionResponse.status = "Please try again!";

                                        return new Result() { result = false, message = "The transaction failed!", data = transactionResponse };

                                    }

                                    Thread.Sleep(1000);
                                    count++;

                                    Console.WriteLine("Still waiting on : " + count);
                                }

                            }

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
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log("" + ex1);
                    }

                    try
                    {
                        if (binaryReader != null)
                            binaryReader.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }

                    try
                    {
                        if (posClient != null && posClient.Connected)
                            posClient.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }
                }

                return new Result() { result = false, message = "Transaction Failed!!!", data = transactionResponse };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result() { result = false, message = "Logon Failed! [" + ex.Message + "]", data = transactionResponse };
            }
            finally
            {

            }

        }

        public Result DoTMS(string merchant, string terminal)
        {
            return new Result()
            {
                result = false,
                message = "Logon Failed!"
            };
        }

        static private byte CalCheckSum(byte[] _PacketData, int PacketLength)
        {
            Byte _CheckSumByte = 0x00;
            for (int i = 0; i < PacketLength; i++)
                _CheckSumByte ^= _PacketData[i];
            return _CheckSumByte;
        }

        private string ReformatPrice(decimal price)
        {
            return price.ToString().Replace(".", "");
        }

        public Result GetTerminalStatus(string merchant, string terminal)
        {
            int bufferSize = 100000;

            //sample based on doc
            //00-19-30-30-30-30-30-30-30-30-30-31-33-30-38-31-30-31-30-1C-26

            byte[] byteList = new byte[] { };
            try
            {
                if (ECR_TYPE == 1)
                {
                    byteList = new byte[] {
                        0x00,
                        0x19,

                        0x30,0x30, //NO NEED ECN
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,

                        0x35,0x35,//Command Type for settlement 55

                        0x30,0x30, //VERSION CODE TO 0
                        0x30,
                        0x1C,
                        0x2B
                    };

                }
                else if (ECR_TYPE == 2)
                {

                    byteList = new byte[] {
                        0x00,
                        0x19,

                        0x30,0x30, //ECN WILL BE REPLACE BY NEW BYTE
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x32,0x30,
                        0x39,0x30,

                        0x35,0x36,//Command Type for settlement 56

                        0x30,0x31,
                        0x30,
                        0x1C,
                        0x2B
                    };

                    Helper a = new Helper();
                    uniqueId = a.Get12Digits();
                    //Console.WriteLine("Unique ID : " + uniqueId);

                    int i = 13; //transfered from uniqueid
                    foreach (char ch in uniqueId.Reverse())
                    {
                        byteList[i] = Convert.ToByte(ch);
                        i--;
                    }

                }

                //end set bytelist

                //collect LRC
                byte[] LRC = new byte[18];
                int y = 0;
                for (var g = 2; g < (byteList.Length - 1); g++)
                {
                    LRC[y] = Convert.ToByte(byteList[g]);
                    y++;
                }

                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {

                    payLoadInStr.Append(b.ToString("x2") + " ");
                }

                //Logger.Log("Item of LRC :\n" + payLoadInStr.ToString());

                byte lrc = CalCheckSum(LRC, LRC.Count());

                //set LRC to byteList
                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

                //prepare send to terminal
                TcpClient posClient = new TcpClient(IP_TERMINAL, PORT_TERMINAL);
                BinaryWriter binaryWriter = null;
                BinaryReader binaryReader = null;
                Stream terminalStream = null;


                try
                {
                    if (posClient.Connected)
                    {
                        terminalStream = posClient.GetStream();


                        if (terminalStream != null)
                        {
                            posClient.ReceiveTimeout = 60000;
                            posClient.SendBufferSize = bufferSize;
                            posClient.ReceiveBufferSize = bufferSize;

                            terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                            terminalStream.WriteTimeout = 30000;

                            binaryWriter = new BinaryWriter(terminalStream);
                            binaryWriter.Write(byteList);


                            Logger.Log("Sent. Waiting for the response...");

                            Thread.Sleep(500);

                            binaryReader = new BinaryReader(terminalStream);

                            terminalStream.Read(byteList, 0, byteList.Length);

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, "GET TERMINAL STATUS");


                            if (string.IsNullOrEmpty(isSuccessful) == false)
                            {

                                return new Result() { result = true, message = "Terminal Connection Succesfully", data = transactionResponse };
                            }
                            else
                            {
                                return new Result() { result = false, message = "The transaction failed!!", data = transactionResponse };
                            }
                            /*else if (isSuccessful.Equals("RUN AGAIN"))
                            {
                                return new Result() { result = false, message = "The transaction failed!" };
                            }*/
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
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log("" + ex1);
                    }

                    try
                    {
                        if (binaryReader != null)
                            binaryReader.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }

                    try
                    {
                        if (posClient != null && posClient.Connected)
                            posClient.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }
                }

                return new Result() { result = false, message = "Transaction Failed!!!", data = transactionResponse };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result() { result = false, message = "Logon Failed! [" + ex.Message + "]", data = transactionResponse };
            }
            finally
            {

            }
        }

        public Result GetLastApprovedTransaction(string merchant, string terminal, string orderNo, decimal amount, string commandType)
        {
            int bufferSize = 100000;

            //sample based on doc
            //00-19-30-30-30-30-30-30-30-30-30-31-33-30-38-31-30-31-30-1C-26

            byte[] byteList = new byte[] { };
            try
            {
                if (ECR_TYPE == 1)
                {
                    byteList = new byte[] {
                        0x00,
                        0x19,

                        0x30,0x30, //NO NEED ECN
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,

                        0x38,0x31,//Command Type for settlement 81

                        0x30,0x30, //VERSION CODE TO 0
                        0x30,
                        0x1C,
                        0x2B
                    };

                }
                else if (ECR_TYPE == 2)
                {
                    byteList = new byte[] {
                        0x00,
                        0x19,

                        0x30,0x30, //ECN WILL BE REPLACE BY NEW BYTE
                        0x30,0x30,
                        0x30,0x30,
                        0x30,0x30,
                        0x32,0x30,
                        0x39,0x30,

                        0x35,0x36,//Command Type for settlement 56

                        0x30,0x31,
                        0x30,
                        0x1C,
                        0x2B
                    };

                    Helper a = new Helper();
                    uniqueId = a.Get12Digits();
                    //Console.WriteLine("Unique ID : " + uniqueId);

                    int i = 13; //transfered from uniqueid
                    foreach (char ch in uniqueId.Reverse())
                    {
                        byteList[i] = Convert.ToByte(ch);
                        i--;
                    }

                }

                //end set bytelist

                //collect LRC
                byte[] LRC = new byte[18];
                int y = 0;
                for (var g = 2; g < (byteList.Length - 1); g++)
                {
                    LRC[y] = Convert.ToByte(byteList[g]);
                    y++;
                }

                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {

                    payLoadInStr.Append(b.ToString("x2") + " ");
                }

                //Logger.Log("Item of LRC :\n" + payLoadInStr.ToString());

                byte lrc = CalCheckSum(LRC, LRC.Count());

                //set LRC to byteList
                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

                //prepare send to terminal
                TcpClient posClient = new TcpClient(IP_TERMINAL, PORT_TERMINAL);
                BinaryWriter binaryWriter = null;
                BinaryReader binaryReader = null;
                Stream terminalStream = null;


                try
                {
                    if (posClient.Connected)
                    {
                        terminalStream = posClient.GetStream();


                        if (terminalStream != null)
                        {
                            posClient.ReceiveTimeout = 60000;
                            posClient.SendBufferSize = bufferSize;
                            posClient.ReceiveBufferSize = bufferSize;

                            terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                            terminalStream.WriteTimeout = 30000;

                            binaryWriter = new BinaryWriter(terminalStream);
                            binaryWriter.Write(byteList);


                            Logger.Log("Sent. Waiting for the response...");

                            Thread.Sleep(500);

                            binaryReader = new BinaryReader(terminalStream);

                            terminalStream.Read(byteList, 0, byteList.Length);

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, commandType);


                            if (string.IsNullOrEmpty(isSuccessful) == false)
                            {

                                return new Result() { result = true, message = "Terminal Connection Succesfully", data = transactionResponse };
                            }
                            else
                            {
                                return new Result() { result = false, message = "The transaction failed!!", data = transactionResponse };
                            }
                            /*else if (isSuccessful.Equals("RUN AGAIN"))
                            {
                                return new Result() { result = false, message = "The transaction failed!" };
                            }*/
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
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log("" + ex1);
                    }

                    try
                    {
                        if (binaryReader != null)
                            binaryReader.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }

                    try
                    {
                        if (posClient != null && posClient.Connected)
                            posClient.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }
                }

                return new Result() { result = false, message = "Transaction Failed!!!", data = transactionResponse };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result() { result = false, message = "Transaction Failed!!! [" + ex.Message + "]", data = transactionResponse };
            }
            finally
            {

            }
        }

        public Result DoTransaction(string merchant, string terminal, string orderNo, decimal amount, string commandType)
        {

            int bufferSize = 100000;

            string amountInString = ReformatPrice(amount);
            byte[] byteList = new byte[] { };
            payload = String.Empty;
            int am = 39;

            commandType = commandType.ToUpper();

            commandType = commandType.ToUpper();

            try
            {

                switch (commandType)
                {
                    case "NETSDB":

                        byteList = new byte[] {
                            0X00,0X84,
                            0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,//ECN
                            0x33,0x30,//FUNCTION CODE
                            0x30,0x30,//VERSION CODE
                            0x30,
                            0x1C, // end header

                            0x54,0x32,0x00,0x02,0x30,0x31,0x1C,
                            0x34,0x33,0x00,0x01,0x30,0x1C,
                            0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,//amount

                            0x34,0x32,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                            0x48,0x44,0x00,0x13,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0x31,0x32,0x33,0x1C,
                            0x48

                        };

                        am = 48;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[am] = Convert.ToByte(ch);
                            am--;
                        }


                        break;

                    case "NETSQR":
                        byteList = new byte[] {
                           0x00,0x66,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,
                                0x33,0x30,
                                0x30,0x30,
                                0x30,
                                0x1C, //end header

                                0x54,0x32,0x00,0x02,0x30,0x34,0x1C,
                                0x34,0x33,0x00,0x01,0x30,0x1C,
                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x34,0x32,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x77

                        };

                        am = 48;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[am] = Convert.ToByte(ch);
                            am--;
                        }

                        break;
                    case "NETSFP":
                        byteList = new byte[] {
                            0x00,0x51,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,
                                0x32,0x34,
                                0x30,0x30,
                                0x30,
                                0x1C,//end header

                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x48,0x34,0x00,0x10,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0x1C,
                                0x57

                        };

                        am = 35;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[am] = Convert.ToByte(ch);
                            am--;
                        }

                        break;
                    case "NETSCS":
                        byteList = new byte[] {
                            0x00,0x36,
                            //0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,//ecn
                            0x35,0x31, //function code
                            0x30,0x31,
                            0x30,
                            0x1C,//end header

                            0x34,0x30,
                            0x00,0x12,
                            0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30, //amount
                            0x1C,
                            0x48,0x34,0x00,0x10,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,
                            0x1C,
                            0x57

                        };

                        am = 23;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[am] = Convert.ToByte(ch);
                            am--;
                        }

                        break;
                    case "CRCARD":
                        byteList = new byte[] {
                            0X02,
                            0X00,
                            0X43,
                            0X50,
                            0X30,
                            0X56,0X31,0X38,
                            0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X20,0X20,0X20,0X20,0X20,0X20,0X20,0X20,
                            0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X30,0X31,0X30,0X30,//AMOUNT
                            0X30,0X30,0X30,0X30,0X30,0X30,0X03,
                            0x48

                        };

                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[am] = Convert.ToByte(ch);
                            am--;
                        }

                        break;

                }
            }
            catch (Exception ty)
            {
                Console.WriteLine(ty.Message.ToString());

            }


            try
            {

                Helper a = new Helper();
                uniqueId = a.Get12Digits();
               
                //collect LRC
                byte[] LRC = new byte[byteList.Length];
                int y = 0;
                for (var g = 1; g < (byteList.Length - 1); g++)
                {
                    LRC[y] = Convert.ToByte(byteList[g]);

                    y++;
                }

                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {
                    payLoadInStr.Append(b.ToString("x2") + " ");
                }

                //Logger.Log("Item of LRC :\n" + payLoadInStr.ToString());

                byte lrc = CalCheckSum(LRC, LRC.Count());

                //set LRC to byteList
                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);


                //START SEND TO TERMINAL
                TcpClient posClient = new TcpClient(IP_TERMINAL, PORT_TERMINAL);

                if (posClient != null)
                {

                    BinaryWriter binaryWriter = null;
                    BinaryReader binaryReader = null;
                    Stream terminalStream = null;

                    try
                    {

                        if (posClient.Connected)
                        {
                            terminalStream = posClient.GetStream();

                            if (terminalStream != null)
                            {
                                posClient.ReceiveTimeout = 60000;
                                posClient.SendBufferSize = bufferSize;
                                posClient.ReceiveBufferSize = bufferSize;

                                terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                                terminalStream.WriteTimeout = 30000;

                                binaryWriter = new BinaryWriter(terminalStream);

                                binaryWriter.Write(byteList);

                                Logger.Log("Sent. Waiting for the response...");

                                try
                                {
                                    binaryReader = new BinaryReader(terminalStream);

                                    binaryReader.Read(byteList, 0, byteList.Length);

                                    ReceiveEDCSocketMessage(DateTime.Now, orderNo, terminalStream, posClient.Client, commandType);
                                    return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };

                                }
                                catch (Exception t)
                                {
                                    int count = 0;
                                    while (true)
                                    {

                                        binaryReader = new BinaryReader(terminalStream);

                                        binaryReader.Read(byteList, 0, byteList.Length);

                                        string cek = ReceiveEDCSocketMessage(DateTime.Now, orderNo, terminalStream, posClient.Client, commandType);

                                        if (!String.IsNullOrEmpty(cek))
                                        {
                                            return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };

                                        }

                                        if (count > 40)
                                        {

                                            Console.WriteLine("Error Reader : " + t.Message.ToString());

                                            try
                                            {
                                                if (binaryWriter != null)
                                                {
                                                    binaryWriter.Close();
                                                    Console.WriteLine("Binary Writer clear!");
                                                }
                                                else
                                                {
                                                    binaryWriter.Close();
                                                    Console.WriteLine("Binary Writer clear!");
                                                }


                                            }
                                            catch (Exception ex1)
                                            {
                                                Logger.Log("" + ex1);
                                            }

                                            try
                                            {
                                                if (binaryReader != null)
                                                {
                                                    binaryReader.Close();
                                                    Console.WriteLine("Binary Reader clear!");
                                                }
                                                else
                                                {
                                                    binaryReader.Close();
                                                    Console.WriteLine("Binary Reader clear!");
                                                }

                                            }
                                            catch (Exception ex1)
                                            {
                                                Logger.Log(ex1);
                                            }

                                            try
                                            {

                                                posClient.Close();
                                                Console.WriteLine("Machine clear!");

                                            }
                                            catch (Exception ex1)
                                            {
                                                Logger.Log(ex1);
                                            }


                                            //start post to response
                                            transactionResponse = new TransactionResponse();
                                            transactionResponse.status = "Please try again!";

                                            return new Result() { result = false, message = "The transaction failed!", data = transactionResponse };


                                        }

                                        Thread.Sleep(1000);
                                        count++;

                                        Console.WriteLine("Still waiting on : " + count);
                                    }


                                }


                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error Reader : " + ex.Message.ToString());

                        try
                        {
                            if (binaryWriter != null)
                            {
                                binaryWriter.Close();
                                Console.WriteLine("Binary Writer clear!");
                            }
                            else
                            {
                                binaryWriter.Close();
                                Console.WriteLine("Binary Writer clear!");
                            }


                        }
                        catch (Exception ex1)
                        {
                            Logger.Log("" + ex1);
                        }

                        try
                        {
                            if (binaryReader != null)
                            {
                                binaryReader.Close();
                                Console.WriteLine("Binary Reader clear!");
                            }
                            else
                            {
                                binaryReader.Close();
                                Console.WriteLine("Binary Reader clear!");
                            }

                        }
                        catch (Exception ex1)
                        {
                            Logger.Log(ex1);
                        }

                        try
                        {

                            posClient.Close();
                            Console.WriteLine("Machine clear!");

                        }
                        catch (Exception ex1)
                        {
                            Logger.Log(ex1);
                        }


                        //start post to response
                        transactionResponse = new TransactionResponse();
                        transactionResponse.status = "Please try again!";

                    }
                    finally
                    {
                        try
                        {
                            if (binaryWriter != null)
                            {
                                binaryWriter.Close();
                                Console.WriteLine("Binary Writer clear!");
                            }
                            else
                            {
                                binaryWriter.Close();
                                Console.WriteLine("Binary Writer clear!");
                            }


                        }
                        catch (Exception ex1)
                        {
                            Logger.Log("" + ex1);
                        }

                        try
                        {
                            if (binaryReader != null)
                            {
                                binaryReader.Close();
                                Console.WriteLine("Binary Reader clear!");
                            }
                            else
                            {
                                binaryReader.Close();
                                Console.WriteLine("Binary Reader clear!");
                            }

                        }
                        catch (Exception ex1)
                        {
                            Logger.Log(ex1);
                        }

                        try
                        {

                            posClient.Close();
                            Console.WriteLine("Machine clear!");

                        }
                        catch (Exception ex1)
                        {
                            Logger.Log(ex1);
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Machine is in use.");
                    transactionResponse = new TransactionResponse();

                    transactionResponse.status = "EDC Machine is buzy.please try again!";

                    return new Result() { result = false, message = "The transaction failed!", data = transactionResponse };

                }

                return new Result() { result = false, message = "Transaction Failed!!!", data = transactionResponse };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result() { result = false, message = "Transaction Failed ! [" + ex.Message + "]", data = transactionResponse };
            }
            finally
            {

            }
        }

        public Result DoContinueSakuku(decimal amount, long refNo)
        {
            return null;

        }

        public Result DoInquirySakuku(decimal amount, long refNo)
        {
            return null;
        }

        public Result DoSettlement(string merchant, string terminal, string orderNo, decimal amount, string commandType, string acquirer_bank)
        {
            int bufferSize = 100000;
            
            //do not send unique ECN
            byte[] byteList = new byte[] { };
            byte[] LRC = new byte[45];
            int y = 0;

            try
            {

                switch (acquirer_bank)
                {
                    case "NETS":
                        byteList = new byte[] {
                            0x00,
                            0x19,

                            0x30,0x30, //NO NEED ECN
                            0x30,0x30,
                            0x30,0x30,
                            0x30,0x30,
                            0x30,0x30,
                            0x30,0x30,

                            0x38,0x31,//Command Type for settlement 81

                            0x30,0x30, //VERSION CODE TO 0
                            0x30,
                            0x1C,
                            0x2B
                        };
                        //collect LRC
                        LRC = new byte[18];
                        
                        for (var g = 2; g < (byteList.Length - 1); g++)
                        {
                            LRC[y] = Convert.ToByte(byteList[g]);
                            y++;
                        }

                        break;
                    case "OCBC":
                        byteList = new byte[] {
                            0x02,0x00,
                            0x43,0x50,0x53,0x56,0x31,0x38,0x32,0x30,0x31,0x35,
                            0x30,0x35,0x32,0x36,0x31,0x34,0x30,0x31,0x30,0x30,
                            0x20,0x20,0x20,0x20,0x20,0x20,0x30,0x30,0x30,0x20,
                            0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                            0x20,0x20,0x20,0x20,0x03,
                            0x0F
                        };

                        //collect LRC

                        LRC = new byte[45];
                        for (var g = 2; g < (byteList.Length - 1); g++)
                        {
                            LRC[y] = Convert.ToByte(byteList[g]);
                            y++;
                        }

                        break;
                    default:

                        break;
                }
                

                
                

                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {

                    payLoadInStr.Append(b.ToString("x2") + " ");
                }

                //Logger.Log("Item of LRC :\n" + payLoadInStr.ToString());

                byte lrc = CalCheckSum(LRC, LRC.Count());

                //set LRC to byteList
                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

                //prepare send to terminal
                TcpClient posClient = new TcpClient(IP_TERMINAL, PORT_TERMINAL);
                BinaryWriter binaryWriter = null;
                BinaryReader binaryReader = null;
                Stream terminalStream = null;


                try
                {
                    if (posClient.Connected)
                    {
                        terminalStream = posClient.GetStream();


                        if (terminalStream != null)
                        {
                            posClient.ReceiveTimeout = 60000;
                            posClient.SendBufferSize = bufferSize;
                            posClient.ReceiveBufferSize = bufferSize;

                            terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                            terminalStream.WriteTimeout = 30000;

                            binaryWriter = new BinaryWriter(terminalStream);
                            binaryWriter.Write(byteList);


                            Logger.Log("Sent. Waiting for the response...");

                            Thread.Sleep(500);

                            binaryReader = new BinaryReader(terminalStream);

                            terminalStream.Read(byteList, 0, byteList.Length);

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, commandType);


                            if (string.IsNullOrEmpty(isSuccessful) == false)
                            {

                                return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };
                            }
                            else
                            {
                                return new Result() { result = isSucccessfull, message = "The transaction failed!!", data = transactionResponse };
                            }
                            /*else if (isSuccessful.Equals("RUN AGAIN"))
                            {
                                return new Result() { result = false, message = "The transaction failed!" };
                            }*/
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
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log("" + ex1);
                    }

                    try
                    {
                        if (binaryReader != null)
                            binaryReader.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }

                    try
                    {
                        if (posClient != null && posClient.Connected)
                            posClient.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }
                }

                return new Result() { result = false, message = "Transaction Failed!!!", data = transactionResponse };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result() { result = false, message = "Logon Failed! [" + ex.Message + "]", data = transactionResponse };
            }
            finally
            {

            }
        }

        public Result CancelTransaction(string merchant, string terminal, string orderNo, decimal amount, string paymentType)
        {

            TcpClient posClient = new TcpClient(IP_TERMINAL, PORT_TERMINAL);
            Stream terminalStream = posClient.GetStream();

            BinaryWriter binaryWriter = new BinaryWriter(terminalStream);
            BinaryReader binaryReader = new BinaryReader(terminalStream);

            try
            {
                if (binaryWriter != null)
                {
                    binaryWriter.Close();
                    Console.WriteLine("Binary Writer clear!");
                }
                else
                {
                    binaryWriter.Close();
                    Console.WriteLine("Binary Writer clear!");
                }


            }
            catch (Exception ex1)
            {
                Logger.Log("" + ex1);
            }

            try
            {
                if (binaryReader != null)
                {
                    binaryReader.Close();
                    Console.WriteLine("Binary Reader clear!");
                }
                else
                {
                    binaryReader.Close();
                    Console.WriteLine("Binary Reader clear!");
                }

            }
            catch (Exception ex1)
            {
                Logger.Log(ex1);
            }

            try
            {
                if (posClient != null && posClient.Connected)
                {
                    posClient.Close();
                    Console.WriteLine("Machine clear!");
                }
                else
                {
                    posClient.Close();
                    Console.WriteLine("Machine clear!");
                }

            }
            catch (Exception ex1)
            {
                Logger.Log(ex1);
            }


            return new Result() { result = false, message = "Cancel Transaction", data = null };



        }
        private void LogPayLoad(byte[] bytes)
        {
            if (bytes.Count() > 0)
            {
                Logger.Log("Request Number of bytes :" + bytes.Count());

                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in bytes)
                {

                    payLoadInStr.Append(b.ToString("x2") + " ");
                }
                Logger.Log("Requested : >>>>> :\n" + payLoadInStr.ToString());
                payload = null;
                payload = payLoadInStr.ToString();


            }

        }

        public static string ReceiveEDCSocketMessage(DateTime sendingTime, String session, Stream terminalStream, Socket socket, String commandType)
        {
            int iCount = 1;

            var intSocketLength = 1000000;

            int encodeType = 1;

            socket.ReceiveBufferSize = 100000;
            socket.SendBufferSize = 100000;

            Encoding ascii = Encoding.ASCII;
            Encoding utf8 = Encoding.UTF8;
            Encoding unicode = Encoding.Unicode;

            bool isReceivedACK = false;

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

                                pieceOfData = ascii.GetString(bytes, 0, bytesRec);

                                break;
                            //UTF8
                            case 2:

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

                    }
                    else
                        pieceOfData = string.Empty;

                    var currentTimestamp = DateTime.Now;
                    var diffInSeconds = (currentTimestamp - sendingTime).TotalSeconds;

                    if (pieceOfData != null && pieceOfData.Length > 0)
                    {

                        ProcessStringResponse(pieceOfData, session, commandType);

                        Thread.Sleep(100);

                        int y = 0;
                        while (true)
                        {
                            if (isReceivedACK == false && pieceOfData.Contains(((char)0x06).ToString()))
                            {
                                Logger.Log("ACK Received after " + diffInSeconds + " seconds.");
                                isReceivedACK = true;
                                break;
                            }

                            Console.WriteLine("Waiting ACK in " + y);

                            if (y > 10)
                            {

                                break;
                            }

                            y++;


                        }


                        finalResposne += pieceOfData;

                        return ProcessResponse(finalResposne);
                    }


                    Thread.Sleep(100);

                    iCount++;


                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return ProcessResponse(finalResposne);

        }

        private static void ProcessStringResponse(string dataResponse, string session, String commandType)
        {
            List<char> chList = dataResponse.ToList();

            Logger.Log("Response Number of bytes :" + chList.Count());

            string byteString = string.Empty;
            string str = string.Empty;


            foreach (char ch in chList)
            {
                byteString += Convert.ToByte(ch).ToString("x2") + " ";

            }

            Logger.Log("\nData Response in Bytes: \n" + byteString);
            Logger.Log("\nData Response in String : \n" + dataResponse);

            ResponseText = byteString;

            string[] tokens = ResponseText.Split(' ');

            String trxType = string.Empty;
            String trxDate = string.Empty;
            String trxTime = string.Empty;
            String expDate = string.Empty;
            String cardPan = string.Empty;
            String invoiceNo = string.Empty;
            String respCode = string.Empty;
            String RRN = string.Empty;
            String authCode = string.Empty;
            String merchantId = string.Empty;
            String terminalId = string.Empty;
            String cardHolder = string.Empty;
            String stan = string.Empty;
            String issuerId = string.Empty;
            String issuerName = string.Empty;
            String posId = string.Empty;

            //start post to response
            transactionResponse = new TransactionResponse();
            commandType = commandType.ToUpper();

            transactionResponse.STAN = 0;
            transactionResponse.AID = "";
            transactionResponse.APP = "";
            transactionResponse.AuthCode = "";
            transactionResponse.batchNo = "";
            transactionResponse.CardPAN = "";
            transactionResponse.CardType = "";
            transactionResponse.ecrTrxNo = "";
            transactionResponse.expireDate = "";
            transactionResponse.ID = "";
            transactionResponse.invoiceNo = "";
            transactionResponse.MerchantId = "";
            transactionResponse.Receipt = "";
            transactionResponse.result = "";
            transactionResponse.surcharge = 0;
            transactionResponse.TC = "";
            transactionResponse.TerminalID = "";
            transactionResponse.TVR = "";
            transactionResponse.TxnRef = "";


            Logger.Log("***************************************************************");
            Logger.Log(" <<<<<<<<<<<<<<<<<<< SUMMARY RESPONSE <<<<<<<<<<<<<<<<<<<");

            var dt5 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            transactionResponse.BankDateTime = transactionResponse.BankDateTime = DateTime.Parse(dt5);

            switch (commandType)
            {
                case "LOGON":
                    if (dataResponse.Contains("ABORTED") || dataResponse.Contains("DECLINED"))
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "DECLINED";
                        transactionResponse.status = "DECLINED";

                    }
                    else
                    {
                        isSucccessfull = true;
                        transactionResponse.result = "APPROVED";
                        transactionResponse.status = "APPROVED";
                        
                    }

                    break;

                case "TMS":

                    transactionResponse.ID = session;

                    transactionResponse.STAN = 0;

                    isSucccessfull = true;
                    transactionResponse.result = "APPROVED";
                    transactionResponse.status = "APPROVED";

                    break;

                case "SETTLEMENT":
                    if (dataResponse.Contains("ABORTED") || dataResponse.Contains("DECLINED"))
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "DECLINED";
                        transactionResponse.status = "DECLINED";

                    }
                    else
                    {
                        isSucccessfull = true;
                        transactionResponse.result = "APPROVED";
                        transactionResponse.status = "APPROVED";

                    }
                    
                    break;

                case "NETSDB":
                    //get auth code
                    authCode = tokens[123];
                    for (int y = 124; y <= 128; y++)
                    {
                        authCode += "-" + tokens[y];
                    }

                    byte[] datacr = FromHex(authCode);
                    string s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("AUTH CODE   :" + s3);
                    transactionResponse.AuthCode = s3;

                    if (s3.Length == 6 && s3 != "000000") //WHY COULD GOT RESPONSE HEADER
                    {
                        isSucccessfull = true;
                        transactionResponse.result = "APPROVED";
                        transactionResponse.status = "APPROVED";

                        //now post session to DB
                        connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                    }
                    else
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "DECLINED";
                        transactionResponse.status = "DECLINED";
                    }

                    //get terminal id
                    terminalId = tokens[79];
                    for (int y = 80; y <= 86; y++)
                    {
                        terminalId += "-" + tokens[y];
                    }
                    datacr = FromHex(terminalId);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("TERMINAL ID :" + s3);
                    transactionResponse.TerminalID = s3;

                    //get merchant ID
                    merchantId = tokens[92];
                    for (int y = 93; y <= 104; y++)
                    {
                        merchantId += "-" + tokens[y];
                    }
                    datacr = FromHex(merchantId);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("MERCHANT ID :" + s3);
                    transactionResponse.MerchantId = s3;

                    //get txnRef
                    RRN = tokens[134];
                    for (int y = 135; y <= 145; y++)
                    {
                        RRN += "-" + tokens[y];
                    }
                    datacr = FromHex(RRN);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("RRN        :" + s3);
                    transactionResponse.TxnRef = s3;

                    Logger.Log("CARD TYPE        :" + commandType);
                    transactionResponse.CardType = commandType;

                    //get card holder
                    cardHolder = tokens[151];
                    for (int y = 152; y <= 170; y++)
                    {
                        cardHolder += "-" + tokens[y];
                    }
                    datacr = FromHex(cardHolder);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("CARD HOLDER :" + s3);
                    transactionResponse.CardPAN = s3;

                    //get trxDate
                    trxDate = tokens[57];
                    for (int y = 58; y <= 62; y++)
                    {
                        trxDate += "-" + tokens[y];
                    }

                    //get trxTime
                    trxTime = tokens[68];
                    for (int y = 69; y <= 74; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }

                    datacr = FromHex(trxDate);
                    s3 = Encoding.ASCII.GetString(datacr);

                    var a5 = s3.Substring(0, 2);
                    var b5 = s3.Substring(2, 2);
                    var c5 = s3.Substring(4, 2);
                    var DT5 = "20" + a5 + "-" + b5 + "-" + c5;
                    Logger.Log("DATE     :" + DT5);

                    string s4 = Encoding.ASCII.GetString(datacr);

                    datacr = FromHex(trxTime);
                    s4 = Encoding.ASCII.GetString(datacr);
                    var d5 = s4.Substring(0, 2);
                    var e5 = s4.Substring(2, 2);
                    var f5 = s4.Substring(4, 2);
                    var ET5 = d5 + ":" + e5 + ":" + f5;
                    Logger.Log("TIME     :" + ET5);

                    try
                    {
                        //set non TO
                        transactionResponse.BankDateTime = DateTime.Parse(DT5 + " " + ET5);

                    }
                    catch (Exception zx)
                    {

                    }
                    finally
                    {
                        transactionResponse.BankDateTime = DateTime.Parse(dt5);
                    }


                    break;
                case "NETSQR":
                    //get auth code
                    authCode = tokens[141];
                    for (int y = 142; y <= 146; y++)
                    {
                        authCode += "-" + tokens[y];
                    }

                    datacr = FromHex(authCode);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("AUTH CODE   :" + s3);
                    transactionResponse.AuthCode = s3;

                    if (s3.Length == 6 && s3 != "000000") //WHY COULD GOT RESPONSE HEADER
                    {
                        isSucccessfull = true;
                        transactionResponse.result = "APPROVED";
                        transactionResponse.status = "APPROVED";

                        //now post session to DB
                        connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                    }
                    else
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "DECLINED";
                        transactionResponse.status = "DECLINED";
                    }

                    //get terminal id
                    terminalId = tokens[96];
                    for (int y = 97; y <= 104; y++)
                    {
                        terminalId += "-" + tokens[y];
                    }
                    datacr = FromHex(terminalId);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("TERMINAL ID :" + s3);
                    transactionResponse.TerminalID = s3;

                    //get merchant ID
                    merchantId = tokens[110];
                    for (int y = 111; y <= 121; y++)
                    {
                        merchantId += "-" + tokens[y];
                    }
                    datacr = FromHex(merchantId);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("MERCHANT ID :" + s3);
                    transactionResponse.MerchantId = s3;

                    //get txnRef
                    RRN = tokens[130];
                    for (int y = 131; y <= 135; y++)
                    {
                        RRN += "-" + tokens[y];
                    }
                    datacr = FromHex(RRN);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("RRN        :" + s3);
                    transactionResponse.TxnRef = s3;

                    Logger.Log("CARD TYPE        :" + commandType);
                    transactionResponse.CardType = commandType;

                    //get trxDate
                    trxDate = tokens[75];
                    for (int y = 76; y <= 80; y++)
                    {
                        trxDate += "-" + tokens[y];
                    }

                    //get trxTime
                    trxTime = tokens[86];
                    for (int y = 87; y <= 94; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }

                    datacr = FromHex(trxDate);
                    s3 = Encoding.ASCII.GetString(datacr);

                    var a51 = s3.Substring(0, 2);
                    var b51 = s3.Substring(2, 2);
                    var c51 = s3.Substring(4, 2);
                    var DT51 = "20" + a51 + "-" + b51 + "-" + c51;
                    Logger.Log("DATE     :" + DT51);

                    string s41 = Encoding.ASCII.GetString(datacr);

                    datacr = FromHex(trxTime);
                    s41 = Encoding.ASCII.GetString(datacr);
                    var d51 = s41.Substring(0, 2);
                    var e51 = s41.Substring(2, 2);
                    var f51 = s41.Substring(4, 2);
                    var ET51 = d51 + ":" + e51 + ":" + f51;
                    Logger.Log("TIME     :" + ET51);

                    try
                    {
                        //set non TO
                        transactionResponse.BankDateTime = DateTime.Parse(DT51 + " " + ET51);

                    }
                    catch (Exception zx)
                    {

                    }
                    finally
                    {
                        transactionResponse.BankDateTime = DateTime.Parse(dt5);
                    }

                    break;
                case "NETSFP":
                    //get auth code
                    authCode = tokens[296];
                    for (int y = 297; y <= 301; y++)
                    {
                        authCode += "-" + tokens[y];
                    }

                    datacr = FromHex(authCode);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("AUTH CODE   :" + s3);
                    transactionResponse.AuthCode = s3;

                    
                    //get terminal id
                    terminalId = tokens[90];
                    for (int y = 91; y <= 97; y++)
                    {
                        terminalId += "-" + tokens[y];
                    }
                    datacr = FromHex(terminalId);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("TERMINAL ID :" + s3);
                    transactionResponse.TerminalID = s3;

                    //get merchant ID
                    merchantId = tokens[103];
                    for (int y = 104; y <= 114; y++)
                    {
                        merchantId += "-" + tokens[y];
                    }
                    datacr = FromHex(merchantId);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("MERCHANT ID :" + s3);
                    transactionResponse.MerchantId = s3;


                    //get CARDPAN FROM BATCH NUMBER
                    cardPan = tokens[123];
                    for (int y = 124; y <= 134; y++)
                    {
                        cardPan += "-" + tokens[y];
                    }
                    datacr = FromHex(cardPan);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("BATCH NUMBER :" + s3);
                    transactionResponse.batchNo = s3;

                    if (s3 != "000000000000")
                    {
                        respCode = "30-30"; //set to no reply from bank

                        isSucccessfull = true;
                        transactionResponse.result = "APPROVED";
                        transactionResponse.status = getResponseCode("00");
                        //now post session to DB
                        connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());


                    }
                    else
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "FAILED";
                        transactionResponse.status = getResponseCode(s3);
                        /*
                        PostJson(
                            "https://maker.ifttt.com/trigger/backend_alert/with/key/c4VTKqDv6MkrNGjvQISqAh",
                            new template
                            {
                                value1 = "BCA Payment",
                                value2 = ConfigurationManager.AppSettings["PROJECT_NAME"].ToString(),
                                value3 = "Message : Transaction with session " + session + " failed for settlement"
                            });
                        */

                    }



                    //get EXPIRE DATE 
                    expDate = tokens[222];
                    for (int y = 223; y <= 227; y++)
                    {
                        expDate += "-" + tokens[y];
                    }
                    datacr = FromHex(expDate);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("EXPIRE DATE :" + s3);
                    transactionResponse.expireDate = s3;


                    //CHEPAS
                    //get TRX TYPE 
                    trxType = tokens[370];
                    for (int y = 370; y <= 371; y++)
                    {
                        trxType += "-" + tokens[y];
                    }
                    datacr = FromHex(trxType);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("TRX TYPE :" + s3);

                    //get TRX AMOUNT
                    trxDate = tokens[372];
                    for (int y = 373; y <= 383; y++)
                    {
                        trxDate += "-" + tokens[y];
                    }
                    datacr = FromHex(trxDate);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("TRX AMOUNT :" + s3);

                    //get TRX DATE 
                    trxDate = tokens[384];
                    for (int y = 385; y <= 389; y++)
                    {
                        trxDate += "-" + tokens[y];
                    }
                    datacr = FromHex(trxDate);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("TRX DATE :" + s3);

                    //get TRX TIME 
                    trxTime = tokens[390];
                    for (int y = 391; y <= 395; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }
                    datacr = FromHex(trxTime);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("TRX TIME :" + s3);


                    //get PRIOR CARD BALANCE 
                    trxTime = tokens[396];
                    for (int y = 397; y <= 407; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }
                    datacr = FromHex(trxTime);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("PRIOR CARD BALANCE     :" + s3);

                    //get POST CARD BALANCE 
                    trxTime = tokens[408];
                    for (int y = 409; y <= 419; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }
                    datacr = FromHex(trxTime);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("POST CARD BALANCE      :" + s3);

                    //get POST AUTO LOAD AMOUNT 
                    trxTime = tokens[420];
                    for (int y = 421; y <= 431; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }
                    datacr = FromHex(trxTime);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("POST AUTO LOAD AMOUNT  :" + s3);



                    // END CEPAS DATA


                    //get CARD PAN 
                    cardPan = tokens[201];
                    for (int y = 202; y <= 216; y++)
                    {
                        cardPan += "-" + tokens[y];
                    }
                    datacr = FromHex(cardPan);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("CARD NUMBER :" + s3);
                    transactionResponse.CardPAN = s3;

                    
                    //get CARD HOLDER 
                    cardHolder = tokens[307];
                    for (int y = 308; y <= 316; y++)
                    {
                        cardHolder += "-" + tokens[y];
                    }
                    datacr = FromHex(cardHolder);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("CARD HOLDER :" + s3);
                    
                    Logger.Log("CARD TYPE    :" + commandType);
                    transactionResponse.CardType = commandType;
                    
                    datacr = FromHex(trxDate);
                    s3 = Encoding.ASCII.GetString(datacr);

                    var a52 = s3.Substring(0, 2);
                    var b52 = s3.Substring(2, 2);
                    var c52 = s3.Substring(4, 2);
                    var DT52 = "20" + a52 + "-" + b52 + "-" + c52;
                    Logger.Log("Date     :" + DT52);

                    string s42 = Encoding.ASCII.GetString(datacr);

                    datacr = FromHex(trxTime);
                    s42 = Encoding.ASCII.GetString(datacr);
                    var d52 = s42.Substring(0, 2);
                    var e52 = s42.Substring(2, 2);
                    var f52 = s42.Substring(4, 2);
                    var ET52 = d52 + ":" + e52 + ":" + f52;
                    Logger.Log("Time     :" + ET52);

                    try
                    {
                        //set non TO
                        transactionResponse.BankDateTime = DateTime.Parse(DT52 + " " + ET52);

                    }
                    catch (Exception zx)
                    {

                    }
                    finally
                    {
                        transactionResponse.BankDateTime = DateTime.Parse(dt5);
                    }


                    break;
                case "CRCARD":
                    if (dataResponse.Contains("MASTER"))
                    {
                        transactionResponse.CardType = "MASTER";
                    }
                    else if (dataResponse.Contains("VISA"))
                    {
                        transactionResponse.CardType = "VISA";
                    }
                    else if (dataResponse.Contains("AMEX"))
                    {
                        transactionResponse.CardType = "AMEX";
                    }
                    else if (dataResponse.Contains("JBC"))
                    {
                        transactionResponse.CardType = "JBC";
                    }
                    else if (dataResponse.Contains("UNION"))
                    {
                        transactionResponse.CardType = "UNION";
                    }
                    else
                    {
                        transactionResponse.CardType = "OTHER";
                    }

                    //get auth code
                    authCode = tokens[89];
                    for (int y = 90; y <= 94; y++)
                    {
                        authCode += "-" + tokens[y];
                    }

                    datacr = FromHex(authCode);
                    s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                    s3 = s3.Trim();
                    Logger.Log("Auth Code       :" + s3);
                    transactionResponse.AuthCode = s3;

                    if (s3.Length == 6 && s3 != "000000" && s3 != "      " && !String.IsNullOrEmpty(s3)) //WHY COULD GOT RESPONSE HEADER
                    {
                        
                        isSucccessfull = true;
                        transactionResponse.result = "APPROVED";
                        transactionResponse.status = "APPROVED";

                    }

                    Logger.Log("Card Type       :" + transactionResponse.CardType);

                    //get terminal id
                    terminalId = tokens[57];
                    for (int y = 58; y <= 64; y++)
                    {
                        terminalId += "-" + tokens[y];
                    }

                    datacr = FromHex(terminalId);
                    s3 = Encoding.ASCII.GetString(datacr);
                    Logger.Log("Terminal Id     :" + s3);
                    transactionResponse.TerminalID = s3;


                    //get card pan
                    cardPan = tokens[65];
                    for (int y = 66; y <= 80; y++)
                    {
                        cardPan += "-" + tokens[y];
                    }

                    var datacs = FromHex(cardPan);
                    string s5 = Encoding.ASCII.GetString(datacs);
                    transactionResponse.CardPAN = s5;
                    Logger.Log("CARD PAN        :" + s5);

                    if (dataResponse.Contains("ABORTED") || dataResponse.Contains("DECLINED"))
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "DECLINED";
                        transactionResponse.status = "DECLINED";

                    }

                    if (dataResponse.Contains("UNSUPPORTED") )
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "DECLINED";
                        transactionResponse.status = "DECLINED";

                    }

                    if (isSucccessfull)
                    {
                        //now post session to DB
                        connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());
                    }


                    break;

            }

            
        }

        private static void PostJson(string uri, template postParameters)
        {
            var postData = JsonConvert.SerializeObject(postParameters);
            var bytes = Encoding.UTF8.GetBytes(postData);
            var httpWebRequest = (HttpWebRequest)WebRequest.Create(uri);
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentLength = bytes.Length;
            httpWebRequest.ContentType = "application/json";
            try
            {
                using (var requestStream = httpWebRequest.GetRequestStream())
                {
                    requestStream.Write(bytes, 0, bytes.Count());
                }

                var httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                if (httpWebResponse.StatusCode != HttpStatusCode.OK)
                {
                    var message = string.Format("POST failed. Received HTTP {0}", httpWebResponse.StatusCode);
                    throw new ApplicationException(message);
                }
            }
            catch (Exception r)
            {
            }
        }

        public static string getResponseCode(String y)
        {
            string h = string.Empty;

            switch (y)
            {
                case "00":
                    h = "APPROVED";

                    break;
                case "01":
                    h = "REFER TO NETS";
                    break;
                case "02":
                    h = "REFER TO BANK";
                    break;
                case "03":
                    h = "INVALID TERMINAL";
                    break;
                case "12":
                    h = "INVALID TRANS";
                    break;
                case "13":
                    h = "INVALID AMOUNT";
                    break;
                case "14":
                    h = "INVALID CARD";
                    break;
                case "19":
                    h = "PLS TRY AGAIN";
                    break;
                case "20":
                    h = "INVALID ADDRESS";
                    break;
                case "21":
                    h = "ADDRESS NOT FOUND";
                    break;
                case "22":
                    h = "FOREIGN ADDRESS";
                    break;
                case "23":
                    h = "INVALID SUP CARD";
                    break;
                case "24":
                    h = "INVALID DDA DATA";
                    break;
                case "25":
                    h = "NO RECORD ON FILE";
                    break;
                case "26":
                    h = "AMOUNT ZERO";
                    break;
                case "27":
                    h = "INVALID AMOUNT";
                    break;
                case "28":
                    h = "INVALID ACCOUNT";
                    break;
                case "29":
                    h = "DUPLICATE DDA";
                    break;
                case "30":
                    h = "INVALID TRANSACTION";
                    break;
                case "31":
                    h = "INVALID TRANSACTION";
                    break;
                case "41":
                    h = "LOST/STOLEN CARD";
                    break;
                case "51":
                    h = "INVALID TRANSACTION";
                    break;
                case "54":
                    h = "EXPIRED CARD";
                    break;
                case "55":
                    h = "INCORRECT PIN";
                    break;
                case "58":
                    h = "INVALID TRANSACTION";
                    break;
                case "61":
                    h = "DAILY LIMIT EXCEEDED";
                    break;
                case "62":
                    h = "INVALID TRANSACTION";
                    break;
                case "63":
                    h = "VOID IMPOSSIBLE";
                    break;
                case "64":
                    h = "TXN ALREADY VOID";
                    break;
                case "75":
                    h = "PIN TRIES EXCEEDED";
                    break;
                case "76":
                    h = "INVALID TRANSACTION";
                    break;
                case "78":
                    h = "INVALID CARD";
                    break;
                case "81":
                    h = "INVALID CARD";
                    break;
                case "85":
                    h = "INVALID CARD";
                    break;
                case "87":
                    h = "DAILY LIMIT EXCEEDED";
                    break;
                case "88":
                    h = "REFUND NOT ALLOWED";
                    break;
                case "91":
                    h = "NO REPLY FROM BANK";
                    break;
                case "V1":
                    h = "REQUEST CRYPTOGRAM DECLINED";
                    break;
                case "V2":
                    h = "ATC DECLINED";
                    break;
                case "V3":
                    h = "CVR DECLINED";
                    break;
                case "V4":
                    h = "TVR DECLINED";
                    break;
                case "V5":
                    h = "FALLBACK DECLINED";
                    break;
                case "V6":
                    h = "PIN REQUIRED";
                    break;
                case "V7":
                    h = "SWITCH TO CONTACT MODE";
                    break;
                case "V8":
                    h = "SUK EXPIRED";
                    break;
                case "C2":
                    h = "CARD LOCKED";
                    break;
                case "C3":
                    h = "EXPIRED CASH CARD";
                    break;
                case "C4":
                    h = "REFUNDED CASH CARD";
                    break;
                case "C5":
                    h = "INVALID TRANSACTION";
                    break;
                case "C6":
                    h = "AMOUNT OVER MAX";
                    break;
                case "C7":
                    h = "INSUFFICIENT FUND";
                    break;
                case "C8":
                    h = "INCORRECT BALANCE";
                    break;
                case "C9":
                    h = "PROBLEM BALANCE";
                    break;
                case "D0":
                    h = "ATU ENABLED";
                    break;
                case "D1":
                    h = "ATU DISABLED";
                    break;
                case "D2":
                    h = "TRANSACTION NOT ALLOWED";
                    break;
                case "D3":
                    h = "INVALID CONTACT NUMBER";
                    break;
                case "D4":
                    h = "INVALID DATE OF BIRTH";
                    break;
                case "D5":
                    h = "AGE LIMIT EXCEEDED";
                    break;
                case "D6":
                    h = "INVALID CARD";
                    break;
                case "D7":
                    h = "REFUND NOT ALLOWED";
                    break;
                case "D8":
                    h = "REFUND AMOUNT GREATER THAN $100";
                    break;
                case "E0":
                    h = "CARD DISABLED";
                    break;
                case "E4":
                    h = "REFER TO NETS";
                    break;
                case "E6":
                    h = "INVALID TRANSACTION";
                    break;
                case "E7":
                    h = "REFER TO NETS";
                    break;
                case "E8":
                    h = "REFER TO NETS";
                    break;
                case "E9":
                    h = "CARD NOT ACTIVATED";
                    break;
                case "F0":
                    h = "REFUNDED CARD";
                    break;
                case "F1":
                    h = "CARD ENABLED";
                    break;
                case "F2":
                    h = "CARD DISABLED";
                    break;
                case "F3":
                    h = "CARD ACTIVATED";
                    break;
                case "F4":
                    h = "DE-REGISTRATION IN PROGRESS";
                    break;
                case "98":
                    h = "MAC ERROR";
                    break;
                case "IM":
                    h = "UNAUTHORISED RESPONSE";
                    break;
                case "IR":
                    h = "INVALID HOST MESSAGE";
                    break;
                case "IT":
                    h = "INVALID TERMINAL";
                    break;
                case "IA":
                    h = "INVALID HOST AMOUNT";
                    break;
                case "IC":
                    h = "INVALID CARD";
                    break;
                case "IL":
                    h = "INVALID DATA LENGTH";
                    break;
                case "TO":
                    h = "TIMEOUT-PLEASE TRY AGAIN";
                    break;
                case "US":
                    h = "CANCELLED BY USER";
                    break;
                case "BF":
                    h = "TRANSACTION BATCH FULL";
                    break;
                case "SC":
                    h = "CASHCARD TRANSACTION UNSUCCESSFUL";
                    break;
                case "N0":
                    h = "DIFFERENT ISSUERS FOR A NETS SALE OR REVALUTION";
                    break;
                case "N1":
                    h = "CASHCARD BLACKLIST";
                    break;
                case "N2":
                    h = "BATCH ALREADY UPLOADED";
                    break;
                case "N3":
                    h = "RESEND BATCH";
                    break;
                case "N4":
                    h = "CASHCARD NOT FOUND";
                    break;
                case "N5":
                    h = "EXPIRED";
                    break;
                case "N6":
                    h = "REFUNDED CASHCARD";
                    break;
                case "N7":
                    h = "CERTIFICATE ERROR";
                    break;
                case "N8":
                    h = "INSUFFICIENT FUNDS/BALANCE";
                    break;
                case "NA":
                    h = "TRANSACTION NOT AVAILABLE";
                    break;
                case "IS":
                    h = "INVALID STAN*";
                    break;
                case "RN":
                    h = "RECORD NOT FOUND";
                    break;
                case "RE":
                    h = "READER NOT READY";
                    break;
                case "T1":
                    h = "INVALID TOP-UP CARD";
                    break;
                case "T2":
                    h = "TERMINAL TOP-UP LIMIT EXCEEDED";
                    break;
                case "T3":
                    h = "RETAILER LIMIT EXCEEDED";
                    break;
                case "LR":
                    h = "MANUAL LOGON REQUIRED** ";
                    break;

                case "DK":
                    h = "BLOCKLIST DOWNLOAD REQUIRED ";
                    break;
                case "DS":
                    h = "CASHCARD SETTLEMENT REQUIRED";
                    break;
                case "BP":
                    h = "CARD BLOCKED";
                    break;
                case "BA":
                    h = "TRANSACTION BLOCKED, to prevent Autoload";
                    break;
                case "GB":
                    h = "Golden Bullet Card";
                    break;
                default:
                    h = "DECLINED";
                    break;
            }

            return h;
        }


        public static string getIssuerId(String y)
        {
            string h = string.Empty;

            switch (y)
            {
                case "00":
                    h = "FLAZZ";
                    break;
                case "01":
                    h = "VISA";
                    break;
                case "02":
                    h = "MASTER";
                    break;
                case "03":
                    h = "AMEX";
                    break;
                case "04":
                    h = "DINERS";
                    break;
                case "05":
                    h = "JCB";
                    break;
                case "06":
                    h = "UNIONPAY";
                    break;
                case "07":
                    h = "BCACARD";
                    break;
                case "08":
                    h = "SAKUKU";
                    break;
                default:
                    h = "OTHER";
                    break;
            }

            return h;
        }

        public static byte[] FromHex(string hex)
        {
            hex = hex.Replace("-", "");
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return raw;
        }

        private static void ProcessByteResponse(byte[] bytes)
        {
            Console.WriteLine("Number of bytes:" + bytes.Count());
            Console.WriteLine("\r\n");
            foreach (byte b in bytes)
            {
                Console.Write(b.ToString("x2") + " ");
            }
            Console.WriteLine("\r\n");
        }

        private static string ProcessResponse(string dataResponse)
        {

            //Logger.Log("Processing the final String: " + dataResponse);

            if (dataResponse.Contains("6000000000112000"))
            {
                Logger.Log("The transaction has been processed successfully!!!!");


                Thread.Sleep(2000);


                if (dataResponse.Contains("VISA"))
                    return "VISA";
                else if (dataResponse.Contains("MASTER"))
                    return "MASTER";
                else if (dataResponse.Contains("MASTER"))
                    return "MASTER";
                else if (dataResponse.Contains("JCB"))
                    return "JCB";
                else if (dataResponse.Contains("UNIONPAY"))
                    return "UNIONPAY";
                else if (dataResponse.Contains("TBA"))
                    return "TBA";
                else if (dataResponse.Contains("FLAZZ"))
                    return "FLAZZ";
                else if (dataResponse.Contains("ALLSAKUKU"))
                    return "ALLSAKUKU";
                else if (dataResponse.Contains("DEBIT"))
                    return "DEBIT";
                else
                    return "BCA";

            }
            else
            {
                if (dataResponse.Contains("VISA"))
                    return "VISA";
                else if (dataResponse.Contains("MASTER"))
                    return "MASTER";
                else if (dataResponse.Contains("MASTER"))
                    return "MASTER";
                else if (dataResponse.Contains("JCB"))
                    return "JCB";
                else if (dataResponse.Contains("UNIONPAY"))
                    return "UNIONPAY";
                else if (dataResponse.Contains("TBA"))
                    return "TBA";
                else if (dataResponse.Contains("FLAZZ"))
                    return "FLAZZ";
                else if (dataResponse.Contains("ALLSAKUKU"))
                    return "ALLSAKUKU";
                else if (dataResponse.Contains("DEBIT"))
                    return "DEBIT";
                else
                    return "BCA";

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



        public Result GetLastTransaction()
        {
            int bufferSize = 100000;

            try
            {
                byte[] byteList = new byte[] {
                    0x00,
                    0x19,

                    0x30,0x30,
                    0x30,0x30,
                    0x30,0x30,
                    0x30,0x30,
                    0x32,0x30,
                    0x39,0x30,

                    0x35,0x36,

                    0x30,0x31,
                    0x30,
                    0x1C,
                    0x2B
                };

                Helper a = new Helper();
                uniqueId = a.Get12Digits();
                //Console.WriteLine("Unique ID : " + uniqueId);

                int i = 13; //transfered from uniqueid
                foreach (char ch in uniqueId.Reverse())
                {
                    byteList[i] = Convert.ToByte(ch);
                    i--;
                }

                //end set bytelist

                //collect LRC
                byte[] LRC = new byte[18];
                int y = 0;
                for (var g = 2; g < (byteList.Length - 1); g++)
                {
                    LRC[y] = Convert.ToByte(byteList[g]);
                    y++;
                }

                Logger.Log("Count LRC   : " + LRC.Length);

                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {

                    payLoadInStr.Append(b.ToString("x2") + " ");
                }

                //Logger.Log("Item of LRC : \n" + payLoadInStr.ToString());

                byte lrc = CalCheckSum(LRC, LRC.Count());

                //set LRC to byteList
                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

                //prepare send to terminal
                TcpClient posClient = new TcpClient(IP_TERMINAL, PORT_TERMINAL);
                BinaryWriter binaryWriter = null;
                BinaryReader binaryReader = null;
                Stream terminalStream = null;


                try
                {
                    if (posClient.Connected)
                    {
                        terminalStream = posClient.GetStream();


                        if (terminalStream != null)
                        {
                            posClient.ReceiveTimeout = 60000;
                            posClient.SendBufferSize = bufferSize;
                            posClient.ReceiveBufferSize = bufferSize;

                            terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                            terminalStream.WriteTimeout = 30000;

                            binaryWriter = new BinaryWriter(terminalStream);
                            binaryWriter.Write(byteList);


                            Logger.Log("Sent. Waiting for the response...");

                            Thread.Sleep(500);

                            binaryReader = new BinaryReader(terminalStream);

                            terminalStream.Read(byteList, 0, byteList.Length);

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, null);


                            if (string.IsNullOrEmpty(isSuccessful) == false)
                            {

                                return new Result() { result = true, message = "Terminal Connection Succesfully", data = transactionResponse };
                            }
                            else
                            {
                                return new Result() { result = false, message = "The transaction failed!!", data = transactionResponse };
                            }
                            /*else if (isSuccessful.Equals("RUN AGAIN"))
                            {
                                return new Result() { result = false, message = "The transaction failed!" };
                            }*/
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
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log("" + ex1);
                    }

                    try
                    {
                        if (binaryReader != null)
                            binaryReader.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }

                    try
                    {
                        if (posClient != null && posClient.Connected)
                            posClient.Close();
                    }
                    catch (Exception ex1)
                    {
                        Logger.Log(ex1);
                    }
                }

                return new Result() { result = false, message = "Transaction Failed!!!", data = transactionResponse };
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result() { result = false, message = "Logon Failed! [" + ex.Message + "]", data = transactionResponse };
            }
            finally
            {

            }
        }

    }
}

