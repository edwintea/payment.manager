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
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Tabsquare.Payment
{
    
    public class BCAImplementation : PaymentInterface
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
            try
            {

            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return new Result() { result = false, message = "Logon Failed! [" + ex.Message + "]", data = transactionResponse };
            }

            return new Result() { result = false, message = "Logon Failed!!!", data = transactionResponse };
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
            for (int i = 1; i < PacketLength; i++)
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
                    
                     Helper key = new Helper();
                     uniqueId = key.Get12Digits();

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

        public Result DoTransaction(string merchant, string terminal, string orderNo, decimal amount, string paymentType)
        {
            
            int bufferSize = 100000;
            
            string amountInString = ReformatPrice(amount);
            byte[] byteList = new byte[] { };
            payload = String.Empty;

            LogPayLoad(byteList);

            try
            {
                if (paymentType == "sakuku")
                {
                    byteList = new byte[] { 0x02,
                        0x01, 0x50,

                        0x01,

                        0x32,0x36,// command here sakuku

                        0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,//AMOUNT

                        0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,//OTHER AMOUNT

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//PAN

                        0x20,0x20,0x20,0x20,//ExpiryDate

                        0x30,0x30,//CancelReason

                        0x30,0x30,0x30,0x30,0x30,0x30,//InvoiceNumber

                        0x20,0x20,0x20,0x20,0x20,0x20,//AuthCode

                        0x20,//InstallmentFlag

                        0x4E,//RedeemFlag

                        0x4E,//DCCFlag

                        0x30,0x30,0x31,//InstallmentPlan

                        0x30,0x33,//InstallmentTenor

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//GENERIC DATA

                        0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,//REFNO

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,

                        0X03,
                        0x0D

                    };

                }
                else
                {
                    byteList = new byte[] { 0x02,
                        0x01, 0x50,

                        0x01,

                        0x30,0x31,// command here sale

                        0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,

                        0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                        0x20,0x20,0x20,0x20,

                        0x30,0x30,

                        0x30,0x30,0x30,0x30,0x30,0x30,

                        0x20,0x20,0x20,0x20,0x20,0x20,

                        0x20,

                        0x20,

                        0x4E,

                        0x20,0x20,0x20,

                        0x20,0x20,

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,

                        0X03,
                        0x1D

                    };

                }

                int i = 17; //transfered from amount based on position
                foreach (char ch in amountInString.Reverse())
                {
                    byteList[i] = Convert.ToByte(ch);
                    i--;
                }

                byte lrc = CalCheckSum(byteList, byteList.Count() - 1);

                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

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

                                Thread.Sleep(300);

                                try
                                {
                                    binaryReader = new BinaryReader(terminalStream);

                                    binaryReader.Read(byteList, 0, byteList.Length);

                                    ReceiveEDCSocketMessage(DateTime.Now, orderNo, terminalStream, posClient.Client, paymentType);

                                }
                                catch (Exception t)
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

                                }

                                if (paymentType == "sakuku")
                                {
                                    return new Result() { result = isSucccessfull, message = "The transaction settled successfully!", nextCommand = "inquiry", data = transactionResponse };

                                }
                                else
                                {
                                    return new Result() { result = isSucccessfull, message = "The transaction settled successfully!", data = transactionResponse };

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
            int bufferSize = 100000;

            string amountInString = ReformatPrice(amount);
            string rrn = refNo.ToString();
            payload = String.Empty;

            byte[] byteList = new byte[] { };
            try
            {
                byteList = new byte[] { 0x02,
                    0x01, 0x50,

                    0x01,

                    0x32,0x37,// command here sakuku

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//transamount

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//other amount

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//pan

                    0x20,0x20,0x20,0x20,//expiry date

                    0x30,0x30,//cancel reason

                    0x30,0x30,0x30,0x30,0x30,0x30,//invoice number

                    0x20,0x20,0x20,0x20,0x20,0x20,//AuthCode

                    0x20,//InstallmentFlag

                    0x20,//RedeemFlag

                    0x4E,//DCCFlag

                    0x20,0x20,0x20,//InstallmentPlan

                    0x20,0x20,//InstallmentTenor

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//generic data

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//ref no 86-97

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,

                    0X03,
                    0x0D

                };

               
                int j = 98; //rrn position
                foreach (char ch in rrn.Reverse())
                {

                    byteList[j] = Convert.ToByte(ch);
                    j--;
                }


                byte lrc = CalCheckSum(byteList, byteList.Count() - 1);

                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

                /*
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


                            terminalStream.ReadTimeout = 30000; 
                            terminalStream.WriteTimeout = 30000;

                            binaryWriter = new BinaryWriter(terminalStream);
                            binaryWriter.Write(byteList);

                            Logger.Log("Sent. Waiting for the response...");

                            Thread.Sleep(2000);

                            binaryReader = new BinaryReader(terminalStream);

                            binaryReader.Read(byteList, 0, byteList.Length);

                            ReceiveEDCSocketMessage(DateTime.Now, "0", terminalStream, posClient.Client, "sakuku");

                            
                            Thread.Sleep(2000);
                            return new Result() { result = isSucccessfull, message = "The transaction settled successfully!", data = transactionResponse };

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
                */
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

                                Thread.Sleep(300);

                                binaryReader = new BinaryReader(terminalStream);

                                binaryReader.Read(byteList, 0, byteList.Length);

                                Thread.Sleep(300);

                                ReceiveEDCSocketMessage(DateTime.Now, "0", terminalStream, posClient.Client, "sakuku");

                                Thread.Sleep(300);


                                //extract response here before parse to response kiosk

                                return new Result() { result = isSucccessfull, message = "The transaction settled successfully!", data = transactionResponse };

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
            
        }

        public Result DoInquirySakuku(decimal amount, long refNo)
        {
            int bufferSize = 100000;

            string amountInString = ReformatPrice(amount);
            string rrn = refNo.ToString();


            byte[] byteList = new byte[] { };
            try
            {
                byteList = new byte[] { 0x02,
                    0x01, 0x50,

                    0x01,

                    0x32,0x38,// command here sakuku

                    0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,

                    0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                    0x30,0x30,0x30,0x30,

                    0x30,0x30,

                    0x30,0x30,0x30,0x30,0x30,0x30,

                    0x20,0x20,0x20,0x20,0x20,0x20,

                    0x20,

                    0x20,

                    0x4E,

                    0x20,0x20,0x20,

                    0x20,0x20,

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//ref no 86-98

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,

                    0X03,
                    0x1D

                };


                int i = 17; //transfered from amount based on position
                foreach (char ch in amountInString.Reverse())
                {
                    byteList[i] = Convert.ToByte(ch);
                    i--;
                }


                int j = 98; //rrn position
                foreach (char ch in rrn.Reverse())
                {

                    byteList[j] = Convert.ToByte(ch);
                    j--;
                }


                byte lrc = CalCheckSum(byteList, byteList.Count() - 1);

                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

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

                                Thread.Sleep(300);

                                binaryReader = new BinaryReader(terminalStream);

                                binaryReader.Read(byteList, 0, byteList.Length);

                                Thread.Sleep(300);

                                ReceiveEDCSocketMessage(DateTime.Now, "0", terminalStream, posClient.Client, "sakuku");

                                Thread.Sleep(300);


                                //extract response here before parse to response kiosk

                                return new Result() { result = isSucccessfull, message = "The transaction settled successfully!", data = transactionResponse };

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

        public Result DoSettlement(string merchant, string terminal, string orderNo, decimal amount, string paymentType, string acquirer_bank)
        {
            int bufferSize = 100000;

            string amountInString = ReformatPrice(amount);

            try
            {

                byte[] byteList = new byte[] { 0x02,
                    0x01, 0x50,

                    0x01,

                    0x31,0x30,

                    0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,

                    0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                    0x20,0x20,0x20,0x20,

                    0x30,0x30,

                    0x30,0x30,0x30,0x30,0x30,0x30,

                    0x20,0x20,0x20,0x20,0x20,0x20,

                    0x20,

                    0x20,

                    0x4E,

                    0x20,0x20,0x20,

                    0x20,0x20,

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,

                    0X03,
                    0x1D

                };


                int i = 17;
                foreach (char ch in amountInString.Reverse())
                {

                    byteList[i] = Convert.ToByte(ch);

                    i--;

                }


                byte lrc = CalCheckSum(byteList, byteList.Count() - 1);

                byteList[byteList.Length - 1] = lrc;

                LogPayLoad(byteList);

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

                            Thread.Sleep(300);

                            binaryReader = new BinaryReader(terminalStream);

                            terminalStream.Read(byteList, 0, byteList.Length);

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, orderNo, terminalStream, posClient.Client, paymentType);


                            if (string.IsNullOrEmpty(isSuccessful) == false)
                            {

                                return new Result() { result = true, message = "The transaction settled successfully!", data = transactionResponse };
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
                return new Result() { result = false, message = "Transaction Failed ! [" + ex.Message + "]", data = transactionResponse };
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
            if(bytes.Count() > 0)
            {
                Logger.Log("Request Number of bytes :" + bytes.Count());

                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in bytes)
                {

                    payLoadInStr.Append(b.ToString("x2") + " ");
                }
                Logger.Log("Requested : >>>>> :" + payLoadInStr.ToString());
                payload = null;
                payload = payLoadInStr.ToString();


            }

        }

        public static string ReceiveEDCSocketMessage(DateTime sendingTime, String session, Stream terminalStream, Socket socket, String paymentType)
        {
            int iCount = 1;

            var intSocketLength = 1000000;

            int encodeType = 1;

            socket.ReceiveBufferSize = 100000;
            socket.SendBufferSize = 100000;

            Logger.Log("Processing...");

            Encoding ascii = Encoding.ASCII;
            Encoding utf8 = Encoding.UTF8;
            Encoding unicode = Encoding.Unicode;

            bool isReceivedACK = false;

            string breakingPoint = ((char)0x1c).ToString() + ((char)0x03).ToString();

            string tranId = string.Empty;
            string pieceOfData = string.Empty;
            string finalResposne = string.Empty;

            Thread.Sleep(300);

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

                        ProcessStringResponse(pieceOfData, session, paymentType);

                        Thread.Sleep(300);


                        if (isReceivedACK == false && pieceOfData.Contains(((char)0x06).ToString()))
                        {
                            Logger.Log("ACK Received after " + diffInSeconds + " seconds.");
                            isReceivedACK = true;
                        }

                        finalResposne += pieceOfData;

                        return ProcessResponse(finalResposne);
                    }

                    Thread.Sleep(300);

                    iCount++;


                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }

            return ProcessResponse(finalResposne);

        }

        private static void ProcessStringResponse(string dataResponse, string session, String paymentType)
        {
            List<char> chList = dataResponse.ToList();

            Logger.Log("Response Number of bytes :" + chList.Count());

            string byteString = string.Empty;


            foreach (char ch in chList)
            {

                byteString += Convert.ToByte(ch).ToString("x2") + " ";

            }

            Logger.Log("Data Response in Bytes:" + byteString);
            Logger.Log("Data Response in String :" + dataResponse);

            ResponseText = byteString;
            
            string[] tokens = ResponseText.Split(' ');

            String trxType = string.Empty;
            String trxDate = string.Empty;
            String trxTime = string.Empty;
            String cardPan = string.Empty;
            String respCode = string.Empty;
            String RRN = string.Empty;
            String authCode = string.Empty;
            String merchantId = string.Empty;
            String terminalId = string.Empty;
            String cardHolder = string.Empty;
            String refNo = string.Empty;
            String issuerId = string.Empty;


            //get trx type
            trxType = tokens[4] + "-" + tokens[5];


            //get card pan
            cardPan = tokens[30];
            for (int y = 31; y <= 48; y++)
            {
                cardPan += "-" + tokens[y];
            }


            //get response code
            respCode = tokens[53] + "-" + tokens[54];

            /*
            int cf = 1;
            while (true)
            {
                MessageBox.Show(tokens[53]);

                if (tokens[53] == "30" || tokens[53] == "31")
                {

                    break;
                    
                }

                cf++;
                Thread.Sleep(1000);

            }
            */

            //get RRN
            RRN = tokens[55];
            for (int y = 56; y <= 66; y++)
            {
                RRN += "-" + tokens[y];
            }


            //get auth code
            authCode = tokens[67];
            for (int y = 68; y <= 72; y++)
            {
                authCode += "-" + tokens[y];
            }

            //get trxDate
            trxDate = tokens[73];
            for (int y =74; y <= 80; y++)
            {
                trxDate += "-" + tokens[y];
            }

            //get trxTime
            trxTime = tokens[81];
            for (int y = 82; y <= 86; y++)
            {
                trxTime += "-" + tokens[y];
            }

            //get merchant id
            merchantId = tokens[87];
            for (int y = 88; y <= 101; y++)
            {
                merchantId += "-" + tokens[y];
            }

            //get terminal id
            terminalId = tokens[102];
            for (int y = 103; y <= 109; y++)
            {
                terminalId += "-" + tokens[y];
            }

            //get Cardholder name
            cardHolder = tokens[111];
            for (int y = 112; y <= 136; y++)
            {
                cardHolder += "-" + tokens[y];
            }

            //get Ref id
            refNo = tokens[153];
            for (int y = 154; y <= 158; y++)
            {
                refNo += "-" + tokens[y];
            }


            //get issuer id
            issuerId = tokens[165] + "-" + tokens[166];

            //start post to response
            transactionResponse = new TransactionResponse();

            transactionResponse.ID = session;


            //render
            byte[] data = FromHex(trxType);
            string s = Encoding.ASCII.GetString(data);
            
            Logger.Log(" >>>>>>>>>>> Summary Response");
            Logger.Log("Transaction Type :" + s);

            transactionResponse.CardType = s;

            data = FromHex(cardPan);
            s = Encoding.ASCII.GetString(data);
            
            Logger.Log("Card Pan         :" + s);
            transactionResponse.CardPAN = s;

            data = FromHex(refNo);
            s = Encoding.ASCII.GetString(data).Trim();
            
            Logger.Log("RRN              :" + s);

            if (s != "")
            {
                
                var isNumeric = int.TryParse(s, out int n);

                if (isNumeric)
                {
                    
                    transactionResponse.STAN = long.Parse(s);
                }
                else
                {
                    transactionResponse.STAN = 0;
                }
                
                
            }
            else
            {

                transactionResponse.STAN = 0;
            }


            data = FromHex(authCode);
            s = data.Count() > 0 ? Encoding.ASCII.GetString(data) : "0";
            Logger.Log("Authorization Code :" + s);

            transactionResponse.AuthCode = s;


            data = FromHex(merchantId);
            s = Encoding.ASCII.GetString(data);
            
            Logger.Log("Merchant Id     :" + s);
            transactionResponse.MerchantId = s;

            data = FromHex(terminalId);
            s = Encoding.ASCII.GetString(data);
            
            Logger.Log("Terminal Id     :" + s);
            transactionResponse.TerminalID = s;

            data = FromHex(RRN);
            s = Encoding.ASCII.GetString(data);
            
            Logger.Log("Ref No          :" + s);
            transactionResponse.TxnRef = s;

            data = FromHex(cardHolder);
            s = Encoding.ASCII.GetString(data);
            
            Logger.Log("Card Holder     :" + s);

            data = FromHex(issuerId);
            s = Encoding.ASCII.GetString(data);
           
            Logger.Log("Issuer Id       :" + getIssuerId(s));
            transactionResponse.CardType = getIssuerId(s);

            var dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            
            data = FromHex(trxDate);
            s = Encoding.ASCII.GetString(data);
            var a = s.Substring(0, 4);
            var b = s.Substring(4, 2);
            var c = s.Substring(6, 2);
            var DT = a + "-" + b + "-" + c;
            Logger.Log("Date            :" + DT);
            
            data = FromHex(trxTime);
            s = Encoding.ASCII.GetString(data);
            var d = s.Substring(0, 2);
            var e = s.Substring(2, 2);
            var f = s.Substring(4, 2);
            var ET = d + ":" + e + ":" + f;
            Logger.Log("Time            :" + ET);

            try
            {
                //set non TO
                transactionResponse.BankDateTime = DateTime.Parse(DT + " " + ET);

            }
            catch (Exception zx)
            {

            }
            finally
            {
                transactionResponse.BankDateTime = DateTime.Parse(dt);
            }

            

            data = FromHex(respCode);
            s = Encoding.ASCII.GetString(data);
            
            Logger.Log("Get Response Code :" + getResponseCode(s));

            int z = 1;
            while (true)
            {
                Console.WriteLine("Response Code :" +s);

                if(s != "TO")
                {
                    Console.WriteLine("Last Response Code :" + s+" in "+z+" sec.");
                    break;//exit loop
                }

                Thread.Sleep(300);
                z++;

                if (z > 10)
                {
                    break;
                }

                
            }

            if (s == "00")
            {
                isSucccessfull = true;
                transactionResponse.result = "APPROVED";
                transactionResponse.status = getResponseCode(s);
                //now post session to DB
                connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

            }
            else
            {
                isSucccessfull = false;
                transactionResponse.result = "FAILED";
                transactionResponse.status = getResponseCode(s);
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
                    h = "Approve";
                    break;
                case "51":
                    h = "Declined";
                    break;
                case "54":
                    h = "Decline Expired Card";
                    break;
                case "55":
                    h = "Decline Incorrect PIN";
                    break;
                case "P2":
                    h = "Read Card Error";
                    break;
                case "P3":
                    h = "User press Cancel on EDC";
                    break;
                case "Z3":
                    h = "EMV Card Decline";
                    break;
                case "CE":
                    h = "Connection Error/Line Busy";
                    break;
                case "TO":
                    h = "Connection Timeout";
                    break;
                case "PT":
                    //h = "EDC Problem";//request by bca team , need change to expire carddate : 13-06-2019
                    h = "Expired Card";
                    break;
                case "PS":
                    h = "Settlement Failed";
                    break;
                case "aa":
                    h = "Decline (aa represent two digit alphanumeric value from EDC)";
                    break;
                case "S2":
                    h = "TRANSAKSI GAGAL";
                    break;
                case "S3":
                    h = "TXN BLM DIPROSES MINTA SCAN QR";
                    break;
                case "S4":
                    h = "TXN EXPIRED ULANGI TRANSAKSI";
                    break;
                case "TN":
                    h = "Topup Tunai Not Ready";
                    break;
                case "12":
                    h = "Sakuku is denied";
                    break;
                case "31":
                    h = "Declined"; //tanya ke orang BCA response code 31 apa?
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
                    //h = "FLAZZ";
                    h = "DEBIT";
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
                    //h = "SAKUKU";
                    h = "OTHER";
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

            Logger.Log("Processing the final String: " + dataResponse);

            if (dataResponse.Contains("6000000000112000"))
            {
                Logger.Log("The transaction has been processed successfully!!!!");

                
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
            return null;
        }


    }
}


