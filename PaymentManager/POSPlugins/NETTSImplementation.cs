using Newtonsoft.Json;
using PaymentManager;
using PaymentManager.DataContracts;
using PaymentManager.Utils;
using System;
using System.Collections;
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
    
    public class NETTSImplementation : PaymentInterface
    {

        public enum LogType { Info, Error, Warning };
        public enum TransactionStatus { Paid, Failed, Cancelled };
        public static Queue qt = new Queue();
        protected JavaScriptSerializer _javaScriptSerializer;

        public static TransactionResponse transactionResponse;
        public static string ResponseText;
        public static Boolean isSucccessfull = false;
        private static DBConnect connect = new DBConnect();
        private static string payload = string.Empty;

        public static String IP_TERMINAL = ConfigurationSettings.AppSettings["IP_TERMINAL"];
        public static int PORT_TERMINAL = Convert.ToInt32(ConfigurationSettings.AppSettings["PORT_TERMINAL"]);
        public static int ECR_TYPE = Convert.ToInt32(ConfigurationSettings.AppSettings["ECR_TYPE"]);
        public static string isRequiredDb = ConfigurationSettings.AppSettings["DB_REQUIRED"].ToLower();
        public static String uniqueId = String.Empty;
        public static String CardType = String.Empty;

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
            byte[] byteList= new byte[] { };

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
                else if(ECR_TYPE==2)
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

                    uniqueId = Get12Digits();
                    //Console.WriteLine("Unique ID : "+ uniqueId);

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
                for(var g=2;g < (byteList.Length-1); g++)
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

                byte lrc = CalCheckSum(LRC, LRC.Count() );
                
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

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, "logon");


                            if (string.IsNullOrEmpty(isSuccessful) == false)
                            {

                                return new Result() { result = true, message = "Terminal Connection Succesfully", data = transactionResponse };
                            }
                            else
                            {
                                return new Result() { result = false, message = "The transaction failed!!", data = transactionResponse };
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
            int bufferSize = 100000;
            //FUNCTION CODE 84
            //sample based on doc
            //00-19-30-30-30-30-30-30-30-30-32-31-39-36-38-34-30-31-30-1C-29
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

                        0x38,0x34,//COMMAND TYPE

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

                        0x38,0x34,

                        0x30,0x31,
                        0x30,
                        0x1C,
                        0x2B
                    };

                    uniqueId = Get12Digits();
                    //Console.WriteLine("Unique ID : "+ uniqueId);

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

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client, "tms");


                            if (string.IsNullOrEmpty(isSuccessful) == false)
                            {

                                return new Result() { result = true, message = "Terminal Connection Succesfully", data = transactionResponse };
                            }
                            else
                            {
                                return new Result() { result = false, message = "The transaction failed!!", data = transactionResponse };
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

        public Result DoTransaction(string merchant, string terminal, string orderNo, decimal amount, string commandType)
        {
            CardType = commandType;
            int bufferSize = 100000;
            int loop = 0;
            string amountInString = ReformatPrice(amount);
            byte[] byteList = new byte[] { };
            payload = String.Empty;

            LogPayLoad(byteList);

            commandType = commandType.ToUpper();

            try
            {
                
                switch (commandType)
                {
                    case "NETSDB": //NETS PURCHASE (CARD) command : 30 type :01
                        //00-84-30-30-30-30-30-30-30-30-30-31-33-36-33-30-30-31-30-1C-54-32-00-02-30-31-1C-34-33-00-01-30-1C-34-30-00-12-30-30-30-30-30-30-30-30-31-30-30-30-1C-34-32-00-12-30-30-30-30-30-30-30-30-30-30-30-30-1C-48-44-00-13-31-32-33-34-35-36-37-38-39-30-31-32-33-1C-48

                        if (ECR_TYPE == 1)
                        {
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

                        }
                        else if (ECR_TYPE == 2)
                        {
                            byteList = new byte[] {
                                0X00,0X84,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x31,0x33,0x36,
                                0x33,0x30,
                                0x30,0x31,
                                0x30,
                                0x1C, // end header

                                0x54,0x32,0x00,0x02,0x30,0x31,0x1C,
                                0x34,0x33,0x00,0x01,0x30,0x1C,
                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,//amount

                                0x34,0x32,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x48,0x44,0x00,0x13,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0x31,0x32,0x33,0x1C,
                                0x48

                            };

                        }
                        

                        int i = 48;

                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[i] = Convert.ToByte(ch);
                            i--;
                        }

                        break;

                    case "NETSQR": //NETS PURCHASE (QR CODE) command : 30 type :04

                        //00-66-30-30-30-30-30-30-30-30-30-31-34-39-33-30-30-31-30-1C-54-32-00-02-30-34-1C-34-33-00-01-30-1C-34-30-00-12-30-30-30-30-30-30-30-30-31-30-30-30-1C-34-32-00-12-30-30-30-30-30-30-30-30-30-30-30-30-1C-77
                        
                        if (ECR_TYPE == 1)
                        {
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

                        }
                        else if (ECR_TYPE == 2)
                        {
                            byteList = new byte[] {
                                0x00,0x66,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x31,0x34,0x39,
                                0x33,0x30,
                                0x30,0x34,
                                0x30,
                                0x1C,

                                0x54,0x32,0x00,0x02,0x30,0x34,0x1C,
                                0x34,0x33,0x00,0x01,0x30,0x1C,
                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x34,0x32,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x77
                            };
                            
                        }

                        int j = 48;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[j] = Convert.ToByte(ch);
                            j--;
                        }

                        break;

                    case "NETSFP": //FLASHPAY PURCHASE command:24
                        //00-51-30-30-30-30-30-30-30-30-30-31-32-35-32-34-30-31-30-1C-34-30-00-12-30-30-30-30-30-30-30-30-31-30-30-30-1C-48-34-00-10-31-32-33-34-35-36-37-38-39-30-1C-57

                        if (ECR_TYPE == 1)
                        {
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

                        }
                        else if (ECR_TYPE == 2)
                        {
                            
                            byteList = new byte[] {
                                0x00,0x51,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x31,0x32,0x35,
                                0x32,0x34,
                                0x30,0x31,
                                0x30,
                                0x1C, //end header

                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,
                                0x30,0x30,0x30,0x30,0x1C,
                                0x48,0x34,0x00,0x10,0x31,0x32,0x33,0x34,0x35,0x36,0x37,0x38,0x39,0x30,0x1C,
                                0x57

                            };

                        }
                        
                        int k = 35;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[k] = Convert.ToByte(ch);
                            k--;
                        }

                        break;

                    case "NETSCS": // CASH CARD PURCHASE command: 51
                        //00-51-30-30-30-30-30-30-30-30-30-31-32-35-32-34-30-31-30-1C-34-30-00-12-30-30-30-30-30-30-30-30-31-30-30-30-1C-48-34-00-10-31-32-33-34-35-36-37-38-39-30-1C-57

                        if (ECR_TYPE == 1)
                        {
                            byteList = new byte[] {
                                0x00,0x36,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,
                                0x35,0x31,
                                0x30,0x30,
                                0x30,
                                0x1C,//end header

                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x22
                            };

                        }
                        else if(ECR_TYPE == 2){
                            byteList = new byte[] {
                                0x00,0x36,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x31,0x35,0x34,
                                0x35,0x31,
                                0x30,0x31,
                                0x30,
                                0x1C, //end header

                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30, //amount
                                0x30,0x30,0x30,0x30,0x1C,
                                0x22
                            };
                        }
                        
                        int l = 35;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[l] = Convert.ToByte(ch);
                            l--;
                        }

                        break;

                    case "CRCARD": //CREDT CARD SALE (Function Code "I0"):
                        //00-51-30-30-30-30-30-30-30-30-30-31-32-35-32-34-30-31-30-1C-34-30-00-12-30-30-30-30-30-30-30-30-31-30-30-30-1C-48-34-00-10-31-32-33-34-35-36-37-38-39-30-1C-57
                        //TO DO NEED TO CHECK THE BANK

                        if (ECR_TYPE == 1)
                        {
                            byteList = new byte[] {
                                0x00,0x47,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,
                                0x49,0x30,
                                0x30,0x30,
                                0x30,
                                0x1C, //end header

                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x39,0x47,0x00,0x06,0x44,0x42,0x53,0x20,0x20,0x20,0x1C,
                                0x4F
                            };

                        }
                        else if (ECR_TYPE == 2)
                        {
                            byteList = new byte[] {
                                0x00,0x47,
                                0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x31,0x35,0x35,
                                0x49,0x30,
                                0x30,0x31,
                                0x30,
                                0x1C,//end header

                                0x34,0x30,0x00,0x12,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x1C,
                                0x39,0x47,0x00,0x06,0x44,0x42,0x53,0x20,0x20,0x20,0x1C,
                                0x4F
                            };

                        }
                        
                        int m = 35;
                        foreach (char ch in amountInString.Reverse())
                        {
                            byteList[m] = Convert.ToByte(ch);
                            m--;
                        }

                        break;

                    case "creditcardsettlement": //CREDT CARD SETTLEMENT (Function Code "I5"):
                        //00-51-30-30-30-30-30-30-30-30-30-31-32-35-32-34-30-31-30-1C-34-30-00-12-30-30-30-30-30-30-30-30-31-30-30-30-1C-48-34-00-10-31-32-33-34-35-36-37-38-39-30-1C-57

                        byteList = new byte[] {
                            0x00,0x30,
                            0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x31,0x36,0x36,0x49,0x35,0x30,0x31,0x30,0x1C,
                            0x39,0x47,0x00,0x06,0x44,0x42,0x53,0x20,0x20,0x20,0x1C,
                            0x41
                        };

                        break;
                }

                LogPayLoad(byteList);
                
                uniqueId = Get12Digits();
                //Console.WriteLine("Unique ID   : " + uniqueId);

                if (ECR_TYPE == 2) //CREATE DYNAMIC ECN NO NEED FOR ECR 1
                {
                    int xc = 13; //transfered from uniqueid
                    foreach (char ch in uniqueId.Reverse())
                    {
                        byteList[xc] = Convert.ToByte(ch);

                        xc--;
                    }

                }

                //collect LRC
                byte[] LRC = new byte[byteList.Length];
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

                                Logger.Log("Sent. Waiting for the response ...");
                                
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

                                        string cek=ReceiveEDCSocketMessage(DateTime.Now, orderNo, terminalStream, posClient.Client, commandType);

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

                                        Console.WriteLine("Still waiting on : "+count);
                                    }
                                    
                                }

                                
                            }
                        }
                    }
                    catch (Exception ex)
                    {

                        //Console.WriteLine("Error Reader : " + ex.Message.ToString());
                        Console.WriteLine("Lost Connection to the Terminal ...");

                        int loop1 = 0;
                        while (true)
                        {

                            try
                            {
                                posClient = new TcpClient();
                                var result = posClient.BeginConnect(IP_TERMINAL, PORT_TERMINAL, null, null);

                                result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));

                                if (posClient != null)
                                {
                                    try
                                    {

                                        if (posClient.Connected)
                                        {
                                            terminalStream = posClient.GetStream();

                                            if (terminalStream != null)
                                            {
                                                posClient.ReceiveTimeout = 6000;
                                                posClient.SendBufferSize = bufferSize;
                                                posClient.ReceiveBufferSize = bufferSize;

                                                terminalStream.ReadTimeout = 30000; // set read timeout and write timeout to be 15s
                                                terminalStream.WriteTimeout = 30000;

                                                binaryWriter = new BinaryWriter(terminalStream);

                                                binaryWriter.Write(byteList);

                                                LogPayLoad(byteList);

                                                //Logger.Log("Sent. Waiting for the response ...");

                                                try
                                                {
                                                    binaryReader = new BinaryReader(terminalStream);

                                                    binaryReader.Read(byteList, 0, byteList.Length);

                                                    ReceiveEDCSocketMessage(DateTime.Now, orderNo, terminalStream, posClient.Client, commandType);
                                                    return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };


                                                }
                                                catch (Exception rr)
                                                {

                                                }
                                            }

                                            break;
                                        }


                                    }
                                    catch (Exception p)
                                    {
                                        Console.WriteLine("Lost Connection...");

                                    }
                                }

                            }
                            catch(Exception h)
                            {
                                Console.WriteLine("Lost Connection...");
                            }
                            
                            Thread.Sleep(1000);
                            loop1++;

                            Console.WriteLine("Try to re-connect : " + loop1);

                            if (loop1 == 60)
                            {
                                //kill connection
                                break;
                            }

                        }
                        //clear reader
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

                        //clear writer
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
                    transactionResponse.status = "Could not connect to EDC!";

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

                        0x38,0x31,//Command Type for settlement 81

                        0x30,0x31,
                        0x30,
                        0x1C,
                        0x2B
                    };

                    uniqueId = Get12Digits();
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
                return new Result() { result = false, message = "Logon Failed! [" + ex.Message + "]", data = transactionResponse };
            }
            finally
            {

            }
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

                    uniqueId = Get12Digits();
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

                            string isSuccessful = ReceiveEDCSocketMessage(DateTime.Now, null, terminalStream, posClient.Client,  "GET TERMINAL STATUS");


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

                    uniqueId = Get12Digits();
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
            if(bytes.Count() > 0)
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

                        Thread.Sleep(300);

                        int y = 0;
                        while (true)
                        {
                            if (isReceivedACK == false && pieceOfData.Contains(((char)0x06).ToString()))
                            {
                                Logger.Log("ACK Received after " + diffInSeconds + " seconds.");
                                isReceivedACK = true;
                                Console.WriteLine("Waiting ACK in " + diffInSeconds);

                                break;
                            }


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
            String cardBalance = string.Empty;

            //start post to response
            transactionResponse = new TransactionResponse();
            commandType = commandType.ToUpper();

            Logger.Log("***************************************************************");
            Logger.Log(" <<<<<<<<<<<<<<<<<<< SUMMARY RESPONSE <<<<<<<<<<<<<<<<<<<");
            Logger.Log("Command Type: " + commandType);

            switch (commandType)
            {
                case "LOGON":
                    
                    //get trx type
                    trxType = tokens[4] + "-" + tokens[5];

                    //get response code
                    respCode = tokens[188] + "-" + tokens[189];


                    //get trxDate
                    trxDate = tokens[122];
                    for (int y = 123; y <= 127; y++)
                    {
                        trxDate += "-" + tokens[y];
                    }

                    //get trxTime
                    trxTime = tokens[133];
                    for (int y = 134; y <= 138; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }

                    //get merchant id
                    merchantId = tokens[168];
                    for (int y = 169; y <= 182; y++)
                    {
                        merchantId += "-" + tokens[y];
                    }

                    //get terminal id
                    terminalId = tokens[155];
                    for (int y = 156; y <= 162; y++)
                    {
                        terminalId += "-" + tokens[y];
                    }

                    //get Ref id
                    stan = tokens[144];
                    for (int y = 145; y <= 149; y++)
                    {
                        stan += "-" + tokens[y];
                    }
                    
                    transactionResponse.ID = session;
                    
                    //render
                    byte[] dataLogon = FromHex(trxType);
                    string s = Encoding.ASCII.GetString(dataLogon);
                    
                    dataLogon = FromHex(stan);
                    s = Encoding.ASCII.GetString(dataLogon).Trim();
                    Logger.Log("STAN            :" + s);

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
                    
                    dataLogon = FromHex(merchantId);
                    s = Encoding.ASCII.GetString(dataLogon);

                    Logger.Log("Merchant Id     :" + s);
                    transactionResponse.MerchantId = s;

                    dataLogon = FromHex(terminalId);
                    s = Encoding.ASCII.GetString(dataLogon);

                    Logger.Log("Terminal Id     :" + s);
                    transactionResponse.TerminalID = s;
                    
                    var dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    dataLogon = FromHex(trxDate);
                    s = Encoding.ASCII.GetString(dataLogon);

                    var a = s.Substring(0, 2);
                    var b = s.Substring(2, 2);
                    var c = s.Substring(4, 2);
                    var DT = "20" + a + "-" + b + "-" + c;
                    Logger.Log("Date            :" + DT);

                    dataLogon = FromHex(trxTime);
                    s = Encoding.ASCII.GetString(dataLogon);
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
                    
                    dataLogon = FromHex(respCode);
                    s = Encoding.ASCII.GetString(dataLogon);

                    Logger.Log("Response Code :" + getResponseCode(s));

                    int z = 1;
                    while (true)
                    {
                        Console.WriteLine("Response Code :" + s);

                        if (s != "TO")
                        {
                            Console.WriteLine("Last Response Code :" + s + " in " + z + " sec.");
                            break;//exit loop
                        }

                        Thread.Sleep(1000);
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

                    //get trx type
                    trxType = tokens[4] + "-" + tokens[5];

                    //get response code
                    if(dataResponse.Contains("@Settlement Complete"))
                    {
                        respCode = "30-30";
                    }
                    else
                    {
                        respCode = tokens[188] + "-" + tokens[189];
                    }


                    //get trxDate
                    trxDate = tokens[122];
                    for (int y = 123; y <= 127; y++)
                    {
                        trxDate += "-" + tokens[y];
                    }

                    //get trxTime
                    trxTime = tokens[133];
                    for (int y = 134; y <= 138; y++)
                    {
                        trxTime += "-" + tokens[y];
                    }

                    //get merchant id
                    merchantId = tokens[168];
                    for (int y = 169; y <= 182; y++)
                    {
                        merchantId += "-" + tokens[y];
                    }

                    //get terminal id
                    terminalId = tokens[155];
                    for (int y = 156; y <= 162; y++)
                    {
                        terminalId += "-" + tokens[y];
                    }

                    //get Ref id
                    stan = tokens[144];
                    for (int y = 145; y <= 149; y++)
                    {
                        stan += "-" + tokens[y];
                    }


                    transactionResponse.ID = session;


                    //render
                    byte[] dataSettlement = FromHex(trxType);
                    string s2 = Encoding.ASCII.GetString(dataSettlement);

                    dataSettlement = FromHex(stan);
                    s2 = Encoding.ASCII.GetString(dataSettlement).Trim();
                    Logger.Log("STAN            :" + s2);

                    if (s2 != "")
                    {

                        var isNumeric = int.TryParse(s2, out int n);

                        if (isNumeric)
                        {

                            transactionResponse.STAN = long.Parse(s2);
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

                    dataSettlement = FromHex(merchantId);
                    s2 = Encoding.ASCII.GetString(dataSettlement);

                    Logger.Log("Merchant Id     :" + s2);
                    transactionResponse.MerchantId = s2;

                    dataSettlement = FromHex(terminalId);
                    s2 = Encoding.ASCII.GetString(dataSettlement);

                    Logger.Log("Terminal Id     :" + s2);
                    transactionResponse.TerminalID = s2;

                    var dt2 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                    dataSettlement = FromHex(trxDate);
                    s2 = Encoding.ASCII.GetString(dataSettlement);

                    var a2 = s2.Substring(0, 2);
                    var b2 = s2.Substring(2, 2);
                    var c2 = s2.Substring(4, 2);
                    var DT2 = "20" + a2 + "-" + b2 + "-" + c2;
                    Logger.Log("Date            :" + DT2);

                    dataSettlement = FromHex(trxTime);
                    s = Encoding.ASCII.GetString(dataSettlement);
                    var d2 = s.Substring(0, 2);
                    var e2 = s.Substring(2, 2);
                    var f2 = s.Substring(4, 2);
                    var ET2 = d2 + ":" + e2 + ":" + f2;
                    Logger.Log("Time            :" + ET2);

                    try
                    {
                        //set non TO
                        transactionResponse.BankDateTime = DateTime.Parse(DT2 + " " + ET2);

                    }
                    catch (Exception zx)
                    {

                    }
                    finally
                    {
                        transactionResponse.BankDateTime = DateTime.Parse(dt2);
                    }

                    dataSettlement = FromHex(respCode);
                    s2 = Encoding.ASCII.GetString(dataSettlement);

                    Logger.Log("Response Code :" + getResponseCode(s2));

                    int z2 = 1;
                    while (true)
                    {
                        Console.WriteLine("Response Code :" + s2);

                        if (s2 != "TO")
                        {
                            Console.WriteLine("Last Response Code :" + s2 + " in " + z2 + " sec.");
                            break;//exit loop
                        }

                        Thread.Sleep(1000);
                        z2++;

                        if (z2 > 10)
                        {
                            break;
                        }


                    }

                    if (s2 == "00")
                    {
                        isSucccessfull = true;
                        transactionResponse.result = "APPROVED";
                        transactionResponse.status = getResponseCode(s2);

                    }

                    break;

                case "NETSDB":
                    if (ECR_TYPE == 2)
                    {
                        //get trx type
                        trxType = tokens[4] + "-" + tokens[5];

                        //get card pan
                        cardPan = tokens[30];
                        for (int y = 31; y <= 48; y++)
                        {
                            cardPan += "-" + tokens[y];
                        }

                        //get response code
                        respCode = tokens[736] + "-" + tokens[737];

                        //get RRN
                        RRN = tokens[134];
                        for (int y = 135; y <= 145; y++)
                        {
                            RRN += "-" + tokens[y];
                        }


                        //get auth code
                        authCode = tokens[123];
                        for (int y = 124; y <= 128; y++)
                        {
                            authCode += "-" + tokens[y];
                        }

                        //get trxDate
                        trxDate = tokens[57];
                        for (int y = 58; y <= 62; y++)
                        {
                            trxDate += "-" + tokens[y];
                        }

                        //get trxTime
                        trxTime = tokens[68];
                        for (int y = 69; y <= 73; y++)
                        {
                            trxTime += "-" + tokens[y];
                        }

                        //get merchant id
                        merchantId = tokens[92];
                        for (int y = 93; y <= 106; y++)
                        {
                            merchantId += "-" + tokens[y];
                        }

                        //get terminal id
                        terminalId = tokens[79];
                        for (int y = 80; y <= 86; y++)
                        {
                            terminalId += "-" + tokens[y];
                        }

                        //get Cardholder name
                        cardHolder = tokens[151];
                        for (int y = 152; y <= 170; y++)
                        {
                            cardHolder += "-" + tokens[y];
                        }

                        //get Ref id
                        stan = tokens[112];
                        for (int y = 113; y <= 117; y++)
                        {
                            stan += "-" + tokens[y];
                        }


                        //get issuer id
                        issuerId = tokens[165] + "-" + tokens[166];
                        //transactionResponse.ID = session;


                        //render
                        byte[] data = FromHex(trxType);
                        string s1 = Encoding.ASCII.GetString(data);
                        transactionResponse.CardType = commandType;

                        data = FromHex(cardPan);
                        s1 = Encoding.ASCII.GetString(data);
                        //transactionResponse.CardPAN = s1;

                        data = FromHex(stan);
                        s1 = Encoding.ASCII.GetString(data).Trim();
                        Logger.Log("STAN            :" + s1);

                        if (s1 != "")
                        {
                            var isNumeric = int.TryParse(s1, out int n);

                            if (isNumeric)
                            {
                                transactionResponse.STAN = long.Parse(s1);
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
                        s1 = data.Count() > 0 ? Encoding.ASCII.GetString(data) : "0";
                        Logger.Log("Auth Code       :" + s1);
                        transactionResponse.AuthCode = s1;
                        
                        data = FromHex(merchantId);
                        s1 = Encoding.ASCII.GetString(data);
                        Logger.Log("Merchant Id     :" + s1);
                        transactionResponse.MerchantId = s1;

                        data = FromHex(terminalId);
                        s1 = Encoding.ASCII.GetString(data);
                        Logger.Log("Terminal Id     :" + s1);
                        transactionResponse.TerminalID = s1;

                        data = FromHex(RRN);
                        s1 = Encoding.ASCII.GetString(data).Trim();
                        Logger.Log("RRN             :" + s1);
                        transactionResponse.TxnRef = s1;

                        data = FromHex(cardHolder);
                        s1 = Encoding.ASCII.GetString(data);
                        Logger.Log("Card Holder     :" + s1);

                        data = FromHex(issuerId);
                        s1 = Encoding.ASCII.GetString(data);
                        Logger.Log("Issuer Id       :" + getIssuerId(s1));
                        transactionResponse.CardType = commandType;

                        var dt1 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        data = FromHex(trxDate);
                        s1 = Encoding.ASCII.GetString(data);

                        var a1 = s1.Substring(0, 2);
                        var b1 = s1.Substring(2, 2);
                        var c1 = s1.Substring(4, 2);
                        var DT1 = "20" + a1 + "-" + b1 + "-" + c1;
                        Logger.Log("Date            :" + DT1);

                        data = FromHex(trxTime);
                        s1 = Encoding.ASCII.GetString(data);
                        var d1 = s1.Substring(0, 2);
                        var e1 = s1.Substring(2, 2);
                        var f1 = s1.Substring(4, 2);
                        var ET1 = d1 + ":" + e1 + ":" + f1;
                        Logger.Log("Time            :" + ET1);

                        try
                        {
                            //set non TO
                            transactionResponse.BankDateTime = DateTime.Parse(DT1 + " " + ET1);

                        }
                        catch (Exception zx)
                        {

                        }
                        finally
                        {
                            transactionResponse.BankDateTime = DateTime.Parse(dt1);
                        }



                        data = FromHex(respCode);
                        s1 = Encoding.ASCII.GetString(data);
                        Logger.Log("Response Code :" + getResponseCode(s1));

                        int z1 = 1;
                        while (true)
                        {
                            Console.WriteLine("Response Code :" + s1);

                            if (s1 != "TO")
                            {
                                Console.WriteLine("Last Response Code :" + s1 + " in " + z1 + " sec.");
                                break;//exit loop
                            }

                            Thread.Sleep(1000);
                            z1++;

                            if (z1 > 10)
                            {
                                break;
                            }


                        }

                        if (s1 == "00")
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.status = getResponseCode(s1);

                            if (dataResponse.Contains("TERMINATED"))
                            {
                                isSucccessfull = false;
                                transactionResponse.result = "TERMINATED";
                                transactionResponse.CardType = commandType;
                                transactionResponse.status = getResponseCode("US");

                            }
                            else if (dataResponse.Contains("REMOVED"))
                            {
                                isSucccessfull = false;
                                transactionResponse.result = "REMOVED";
                                transactionResponse.CardType = commandType;
                                transactionResponse.status = getResponseCode("US");
                            }
                            else
                            {
                                if (isRequiredDb == "true")
                                {
                                    //now post session to DB
                                    connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());
                                }

                            }

                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.status = getResponseCode(s1);
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
                    else
                    {
                        if (dataResponse.Contains("APPROVED"))
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }
                    }

                    if (dataResponse.Contains("TERMINATED"))
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "TERMINATED";
                        transactionResponse.CardType = commandType;
                        transactionResponse.status = getResponseCode("US");
                    }

                    if (dataResponse.Contains("REMOVED"))
                    {
                        isSucccessfull = false;
                        transactionResponse.result = "REMOVED";
                        transactionResponse.CardType = commandType;
                        transactionResponse.status = getResponseCode("US");
                    }

                    break;
                case "CRCARD":
                    if (ECR_TYPE == 2)
                    {

                        //get trx type
                        trxType = tokens[215];
                        for (int y = 216; y <= 218; y++)
                        {
                            trxType += "-" + tokens[y];
                        }

                        //get card pan
                        cardPan = tokens[224];
                        for (int y = 225; y <= 239; y++)
                        {
                            cardPan += "-" + tokens[y];
                        }

                        //get RRN
                        RRN = tokens[311];
                        for (int y = 312; y <= 322; y++)
                        {
                            RRN += "-" + tokens[y];
                        }

                        //get RRN
                        posId = tokens[468];
                        for (int y = 469; y <= 475; y++)
                        {
                            posId += "-" + tokens[y];
                        }


                        //get auth code
                        authCode = tokens[145];
                        for (int y = 146; y <= 150; y++)
                        {
                            authCode += "-" + tokens[y];
                        }

                        //get auth code
                        invoiceNo = tokens[156];
                        for (int y = 157; y <= 161; y++)
                        {
                            invoiceNo += "-" + tokens[y];
                        }

                        //get trxDate
                        trxDate = tokens[123];
                        for (int y = 124; y <= 128; y++)
                        {
                            trxDate += "-" + tokens[y];
                        }

                        //get trxTime
                        trxTime = tokens[134];
                        for (int y = 135; y <= 139; y++)
                        {
                            trxTime += "-" + tokens[y];
                        }

                        //get expDate
                        expDate = tokens[291];
                        for (int y = 292; y <= 294; y++)
                        {
                            expDate += "-" + tokens[y];
                        }

                        //get merchant id
                        merchantId = tokens[180];
                        for (int y = 181; y <= 194; y++)
                        {
                            merchantId += "-" + tokens[y];
                        }

                        //get terminal id
                        terminalId = tokens[167];
                        for (int y = 168; y <= 174; y++)
                        {
                            terminalId += "-" + tokens[y];
                        }

                        //get Cardholder name
                        cardHolder = tokens[328];
                        for (int y = 329; y <= 353; y++)
                        {
                            cardHolder += "-" + tokens[y];
                        }

                        //get Ref id
                        stan = tokens[112];
                        for (int y = 113; y <= 117; y++)
                        {
                            stan += "-" + tokens[y];
                        }


                        //get issuer id

                        issuerName = tokens[200];
                        for (int y = 201; y <= 209; y++)
                        {
                            issuerName += "-" + tokens[y];
                        }

                        issuerId = tokens[165] + "-" + tokens[166];

                        transactionResponse.ID = session;


                        //render
                        byte[] datacr = FromHex(trxType);
                        string s3 = Encoding.ASCII.GetString(datacr);
                        Logger.Log("Card Type       :" + s3);

                        datacr = FromHex(cardPan);
                        s3 = Encoding.ASCII.GetString(datacr);
                        transactionResponse.CardPAN = s3;
                        Logger.Log("CARD PAN        :" + s3);


                        datacr = FromHex(stan);
                        s3 = Encoding.ASCII.GetString(datacr).Trim();
                        Logger.Log("STAN            :" + s3);

                        if (s3 != "")
                        {
                            var isNumeric = int.TryParse(s3, out int n);

                            if (isNumeric)
                            {
                                transactionResponse.STAN = long.Parse(s3);
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


                        datacr = FromHex(authCode);
                        s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                        Logger.Log("Auth Code       :" + s3);

                        transactionResponse.AuthCode = s3;

                        if (s3 == "000000")
                        {
                            respCode = "39-31"; //set to no reply from bank

                        }
                        else if (Convert.ToInt32(s3) > 0)
                        {
                            respCode = "30-30";
                        }
                        else
                        {
                            respCode = tokens[481] + "-" + tokens[482];

                        }

                        datacr = FromHex(invoiceNo);
                        s3 = datacr.Count() > 0 ? Encoding.ASCII.GetString(datacr) : "0";
                        Logger.Log("Invoice Number  :" + s3);


                        datacr = FromHex(merchantId);
                        s3 = Encoding.ASCII.GetString(datacr);
                        Logger.Log("Merchant Id     :" + s3);
                        transactionResponse.MerchantId = s3;

                        datacr = FromHex(terminalId);
                        s3 = Encoding.ASCII.GetString(datacr);
                        Logger.Log("Terminal Id     :" + s3);
                        transactionResponse.TerminalID = s3;

                        datacr = FromHex(RRN);
                        s3 = Encoding.ASCII.GetString(datacr).Trim();
                        Logger.Log("RRN             :" + s3);
                        transactionResponse.TxnRef = s3;

                        datacr = FromHex(posId);
                        s3 = Encoding.ASCII.GetString(datacr).Trim();
                        Logger.Log("POS ID          :" + s3);

                        datacr = FromHex(cardHolder);
                        s3 = Encoding.ASCII.GetString(datacr);
                        Logger.Log("Card Holder     :" + s3);

                        datacr = FromHex(issuerId);
                        s3 = Encoding.ASCII.GetString(datacr);
                        Logger.Log("Issuer Id       :" + getIssuerId(s3));

                        datacr = FromHex(issuerName);
                        s3 = Encoding.ASCII.GetString(datacr).Trim();
                        Logger.Log("Issuer Name     :" + s3);
                        transactionResponse.CardType = s3;

                        var dt3 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        datacr = FromHex(trxDate);
                        s3 = Encoding.ASCII.GetString(datacr);

                        var a3 = s3.Substring(0, 2);
                        var b3 = s3.Substring(2, 2);
                        var c3 = s3.Substring(4, 2);
                        var DT3 = "20" + a3 + "-" + b3 + "-" + c3;
                        Logger.Log("Date            :" + DT3);

                        datacr = FromHex(trxTime);
                        s3 = Encoding.ASCII.GetString(datacr);
                        var d3 = s3.Substring(0, 2);
                        var e3 = s3.Substring(2, 2);
                        var f3 = s3.Substring(4, 2);
                        var ET3 = d3 + ":" + e3 + ":" + f3;
                        Logger.Log("Time            :" + ET3);

                        try
                        {
                            //set non TO
                            transactionResponse.BankDateTime = DateTime.Parse(DT3 + " " + ET3);

                        }
                        catch (Exception zx)
                        {

                        }
                        finally
                        {
                            transactionResponse.BankDateTime = DateTime.Parse(dt3);
                        }

                        datacr = FromHex(expDate);
                        s3 = Encoding.ASCII.GetString(datacr);
                        Logger.Log("Expire Date     :" + s3);

                        datacr = FromHex(respCode);
                        s3 = Encoding.ASCII.GetString(datacr);
                        Logger.Log("Response Code :" + getResponseCode(s3));

                        int z3 = 1;
                        while (true)
                        {
                            Console.WriteLine("Response Code :" + s3);

                            if (s3 != "TO")
                            {
                                Console.WriteLine("Last Response Code :" + s3 + " in " + z3 + " sec.");
                                break;//exit loop
                            }

                            Thread.Sleep(1000);
                            z3++;

                            if (z3 > 10)
                            {
                                break;
                            }


                        }

                        if (dataResponse.Contains("MASTER"))
                        {
                            transactionResponse.CardType = "MASTER";

                        }
                        else if (dataResponse.Contains("VISA"))
                        {
                            transactionResponse.CardType = "VISA";
                        }
                        else if (dataResponse.Contains("JCB"))
                        {
                            transactionResponse.CardType = "JCB";
                        }
                        else if (dataResponse.Contains("DINNERS"))
                        {
                            transactionResponse.CardType = "DINNERS";
                        }
                        else if (dataResponse.Contains("UNIONPAY"))
                        {
                            transactionResponse.CardType = "UNIONPAY";
                        }
                        else if (dataResponse.Contains("TBA"))
                        {
                            transactionResponse.CardType = "TBA";
                        }


                        if (s3 == "00")
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.status = getResponseCode(s3);

                            if (dataResponse.Contains("TERMINATED"))
                            {
                                isSucccessfull = false;
                                transactionResponse.result = "TERMINATED";
                                transactionResponse.CardType = commandType;
                                transactionResponse.status = getResponseCode("US");

                            }
                            else if (dataResponse.Contains("REMOVED"))
                            {
                                isSucccessfull = false;
                                transactionResponse.result = "REMOVED";
                                transactionResponse.CardType = commandType;
                                transactionResponse.status = getResponseCode("US");
                            }
                            else
                            {
                                if (isRequiredDb == "true")
                                {
                                    //now post session to DB
                                    connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                                }

                            }


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

                        if (dataResponse.Contains("TERMINATED"))
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "TERMINATED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("US");
                        }

                        if (dataResponse.Contains("REMOVED"))
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "REMOVED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("US");
                        }
                    }
                    else
                    {
                        if (dataResponse.Contains("APPROVED"))
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }

                    }
                    
                    break;
                case "NETSFP":
                    if (ECR_TYPE == 2)
                    {

                        //get trx type
                        trxType = tokens[215];
                        for (int y = 216; y <= 218; y++)
                        {
                            trxType += "-" + tokens[y];
                        }

                        //get card pan
                        cardPan = tokens[201];
                        for (int y = 202; y <= 216; y++)
                        {
                            cardPan += "-" + tokens[y];
                        }


                        //get RRN
                        RRN = tokens[296];
                        for (int y = 297; y <= 307; y++)
                        {
                            RRN += "-" + tokens[y];
                        }

                        //get RRN
                        posId = tokens[222];
                        for (int y = 222; y <= 222; y++)
                        {
                            posId += "-" + tokens[y];
                        }


                        //get auth code
                        authCode = tokens[246];
                        for (int y = 247; y <= 251; y++)
                        {
                            authCode += "-" + tokens[y];
                        }

                        //get auth code
                        invoiceNo = tokens[156];
                        for (int y = 157; y <= 161; y++)
                        {
                            invoiceNo += "-" + tokens[y];
                        }

                        //get trxDate
                        trxDate = tokens[123];
                        for (int y = 124; y <= 128; y++)
                        {
                            trxDate += "-" + tokens[y];
                        }

                        //get trxTime
                        trxTime = tokens[134];
                        for (int y = 135; y <= 139; y++)
                        {
                            trxTime += "-" + tokens[y];
                        }

                        //get expDate
                        expDate = tokens[222];
                        for (int y = 223; y <= 229; y++)
                        {
                            expDate += "-" + tokens[y];
                        }

                        //get merchant id
                        merchantId = tokens[103];
                        for (int y = 104; y <= 117; y++)
                        {
                            merchantId += "-" + tokens[y];
                        }

                        //get terminal id
                        terminalId = tokens[90];
                        for (int y = 91; y <= 97; y++)
                        {
                            terminalId += "-" + tokens[y];
                        }

                        //get Cardholder name
                        cardHolder = tokens[328];
                        for (int y = 329; y <= 353; y++)
                        {
                            cardHolder += "-" + tokens[y];
                        }

                        //get Ref id
                        stan = tokens[140];
                        for (int y = 141; y <= 145; y++)
                        {
                            stan += "-" + tokens[y];
                        }


                        //get issuer id

                        issuerName = tokens[257];
                        for (int y = 258; y <= 266; y++)
                        {
                            issuerName += "-" + tokens[y];
                        }

                        issuerId = tokens[165] + "-" + tokens[166];

                        transactionResponse.ID = session;


                        //render
                        byte[] datafp = FromHex(trxType);
                        string s4 = Encoding.ASCII.GetString(datafp);

                        datafp = FromHex(cardPan);
                        s4 = Encoding.ASCII.GetString(datafp);
                        transactionResponse.CardPAN = s4;
                        Logger.Log("CARD PAN        :" + s4);

                        if (s4 != "0000000000000000")
                        {
                            respCode = "30-30"; //set to no reply from bank

                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.status = getResponseCode("00");

                            if (isRequiredDb == "true")
                            {
                                //now post session to DB
                                connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                            }
                            
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.status = getResponseCode(s4);
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


                        datafp = FromHex(stan);
                        s4 = Encoding.ASCII.GetString(datafp).Trim();
                        Logger.Log("STAN            :" + s4);

                        if (s4 != "")
                        {
                            var isNumeric = int.TryParse(s4, out int n);

                            if (isNumeric)
                            {
                                transactionResponse.STAN = long.Parse(s4);
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


                        datafp = FromHex(authCode);
                        s4 = datafp.Count() > 0 ? Encoding.ASCII.GetString(datafp) : "0";
                        Logger.Log("Auth Code       :" + s4);
                        transactionResponse.AuthCode = s4;
                        

                        datafp = FromHex(invoiceNo);
                        s4 = datafp.Count() > 0 ? Encoding.ASCII.GetString(datafp) : "0";
                        Logger.Log("Invoice Number  :" + s4);


                        datafp = FromHex(merchantId);
                        s4 = Encoding.ASCII.GetString(datafp);
                        Logger.Log("Merchant Id     :" + s4);
                        transactionResponse.MerchantId = s4;

                        datafp = FromHex(terminalId);
                        s4 = Encoding.ASCII.GetString(datafp);
                        Logger.Log("Terminal Id     :" + s4);
                        transactionResponse.TerminalID = s4;

                        datafp = FromHex(RRN);
                        s4 = Encoding.ASCII.GetString(datafp).Trim();
                        Logger.Log("RRN             :" + s4);
                        transactionResponse.TxnRef = s4;

                        datafp = FromHex(posId);
                        s4 = Encoding.ASCII.GetString(datafp).Trim();

                        datafp = FromHex(cardHolder);
                        s4 = Encoding.ASCII.GetString(datafp);
                        Logger.Log("Card Holder     :" + s4);

                        datafp = FromHex(issuerId);
                        s4 = Encoding.ASCII.GetString(datafp);

                        datafp = FromHex(issuerName);
                        s4 = Encoding.ASCII.GetString(datafp).Trim();
                        Logger.Log("Issuer Name     :" + s4);
                        transactionResponse.CardType = commandType;

                        var dt4 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        datafp = FromHex(trxDate);
                        s4 = Encoding.ASCII.GetString(datafp);

                        var a4 = s4.Substring(0, 2);
                        var b4 = s4.Substring(2, 2);
                        var c4 = s4.Substring(4, 2);
                        var DT4 = "20" + a4 + "-" + b4 + "-" + c4;
                        //Logger.Log("Date            :" + DT4);

                        datafp = FromHex(trxTime);
                        s4 = Encoding.ASCII.GetString(datafp);
                        var d4 = s4.Substring(0, 2);
                        var e4 = s4.Substring(2, 2);
                        var f4 = s4.Substring(4, 2);
                        var ET4 = d4 + ":" + e4 + ":" + f4;
                        //Logger.Log("Time            :" + ET4);

                        try
                        {
                            //set non TO
                            transactionResponse.BankDateTime = DateTime.Parse(DT4 + " " + ET4);

                        }
                        catch (Exception zx)
                        {

                        }
                        finally
                        {
                            transactionResponse.BankDateTime = DateTime.Parse(dt4);
                        }

                        datafp = FromHex(expDate);
                        s4 = Encoding.ASCII.GetString(datafp);
                        Logger.Log("Expire Date     :" + s4);

                        datafp = FromHex(respCode);
                        s4 = Encoding.ASCII.GetString(datafp);
                        Logger.Log("Response Code :" + getResponseCode(s4));
                        Logger.Log("Response Code id :" + s4);
                    }
                    else
                    {
                        if (dataResponse.Contains("APPROVED"))
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }

                    }
                    break;

                case "NETSCS":
                    if (ECR_TYPE == 2)
                    {
                        //get trx type
                        trxType = tokens[215];
                        for (int y = 216; y <= 218; y++)
                        {
                            trxType += "-" + tokens[y];
                        }

                        //get card BALANCE
                        //000000000000
                        cardBalance = tokens[280];
                        for (int y = 281; y <= 291; y++)
                        {
                            cardBalance += "-" + tokens[y];
                        }

                        //get card pan
                        cardPan = tokens[160];
                        for (int y = 161; y <= 175; y++)
                        {
                            cardPan += "-" + tokens[y];
                        }

                        //get RRN
                        RRN = tokens[311];
                        for (int y = 312; y <= 313; y++)
                        {
                            RRN += "-" + tokens[y];
                        }

                        //get RRN
                        posId = tokens[222];
                        for (int y = 222; y <= 222; y++)
                        {
                            posId += "-" + tokens[y];
                        }


                        //get auth code
                        authCode = tokens[145];
                        for (int y = 146; y <= 150; y++)
                        {
                            authCode += "-" + tokens[y];
                        }

                        //get auth code
                        invoiceNo = tokens[156];
                        for (int y = 157; y <= 161; y++)
                        {
                            invoiceNo += "-" + tokens[y];
                        }

                        //get trxDate
                        trxDate = tokens[105];
                        for (int y = 106; y <= 110; y++)
                        {
                            trxDate += "-" + tokens[y];
                        }

                        //get trxTime
                        trxTime = tokens[116];
                        for (int y = 117; y <= 121; y++)
                        {
                            trxTime += "-" + tokens[y];
                        }

                        //get expDate
                        expDate = tokens[291];
                        for (int y = 292; y <= 294; y++)
                        {
                            expDate += "-" + tokens[y];
                        }

                        //get terminal id
                        terminalId = tokens[127];
                        for (int y = 128; y <= 134; y++)
                        {
                            terminalId += "-" + tokens[y];
                        }

                        //get merchant id
                        merchantId = tokens[140];
                        for (int y = 141; y <= 154; y++)
                        {
                            merchantId += "-" + tokens[y];
                        }



                        //get Cardholder name
                        cardHolder = tokens[312];
                        for (int y = 313; y <= 314; y++)
                        {
                            cardHolder += "-" + tokens[y];
                        }

                        //get Ref id
                        stan = tokens[112];
                        for (int y = 113; y <= 117; y++)
                        {
                            stan += "-" + tokens[y];
                        }


                        //get issuer id

                        issuerName = tokens[200];
                        for (int y = 201; y <= 209; y++)
                        {
                            issuerName += "-" + tokens[y];
                        }

                        issuerId = tokens[165] + "-" + tokens[166];

                        transactionResponse.ID = session;


                        //render
                        byte[] datacs = FromHex(trxType);
                        string s5 = Encoding.ASCII.GetString(datacs);

                        datacs = FromHex(cardPan);
                        s5 = Encoding.ASCII.GetString(datacs);
                        transactionResponse.CardPAN = s5;
                        Logger.Log("CARD PAN        :" + s5);

                        datacs = FromHex(cardBalance);
                        s5 = Encoding.ASCII.GetString(datacs);

                        if (s5 == "000000000000")
                        {
                            respCode = "39-31"; //set to no reply from bank

                        }
                        else if (Convert.ToInt32(s5) > 0)
                        {
                            respCode = "30-30";
                        }
                        else
                        {
                            respCode = "39-31";

                        }

                        Logger.Log("CARD BALANCE    :" + s5);


                        datacs = FromHex(stan);
                        s5 = Encoding.ASCII.GetString(datacs).Trim();
                        //Logger.Log("STAN            :" + s5);

                        if (s5 != "")
                        {
                            var isNumeric = int.TryParse(s5, out int n);

                            if (isNumeric)
                            {

                                transactionResponse.STAN = long.Parse(s5);
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


                        datacs = FromHex(authCode);
                        s5 = datacs.Count() > 0 ? Encoding.ASCII.GetString(datacs) : "0";
                        //Logger.Log("Auth Code       :" + s5);
                        //NO AUTH CODE FOR THIS COMMAND


                        datacs = FromHex(invoiceNo);
                        s5 = datacs.Count() > 0 ? Encoding.ASCII.GetString(datacs) : "0";
                        //Logger.Log("Invoice Number  :" + s5);


                        datacs = FromHex(merchantId);
                        s5 = Encoding.ASCII.GetString(datacs);
                        Logger.Log("Merchant Id     :" + s5);
                        transactionResponse.MerchantId = s5;

                        datacs = FromHex(terminalId);
                        s5 = Encoding.ASCII.GetString(datacs);
                        Logger.Log("Terminal Id     :" + s5);
                        transactionResponse.TerminalID = s5;

                        datacs = FromHex(RRN);
                        s5 = Encoding.ASCII.GetString(datacs).Trim();
                        //Logger.Log("RRN             :" + s5);
                        //transactionResponse.TxnRef = s5;

                        datacs = FromHex(posId);
                        s5 = Encoding.ASCII.GetString(datacs).Trim();
                        //Logger.Log("POS ID          :" + s5);

                        datacs = FromHex(cardHolder);
                        s5 = Encoding.ASCII.GetString(datacs);
                        //Logger.Log("Card Holder     :" + s5);

                        datacs = FromHex(issuerId);
                        s5 = Encoding.ASCII.GetString(datacs);
                        //Logger.Log("Issuer Id       :" + getIssuerId(s5));

                        datacs = FromHex(issuerName);
                        s5 = Encoding.ASCII.GetString(datacs).Trim();
                        //Logger.Log("Issuer Name     :" + s5);
                        transactionResponse.CardType = commandType;

                        var dt5 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        datacs = FromHex(trxDate);
                        s5 = Encoding.ASCII.GetString(datacs);

                        var a5 = s5.Substring(0, 2);
                        var b5 = s5.Substring(2, 2);
                        var c5 = s5.Substring(4, 2);
                        var DT5 = "20" + a5 + "-" + b5 + "-" + c5;
                        Logger.Log("Date            :" + DT5);

                        string s4 = Encoding.ASCII.GetString(datacs);

                        datacs = FromHex(trxTime);
                        s4 = Encoding.ASCII.GetString(datacs);
                        var d5 = s4.Substring(0, 2);
                        var e5 = s4.Substring(2, 2);
                        var f5 = s4.Substring(4, 2);
                        var ET5 = d5 + ":" + e5 + ":" + f5;
                        Logger.Log("Time            :" + ET5);

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

                        datacs = FromHex(expDate);
                        s5 = Encoding.ASCII.GetString(datacs);
                        //Logger.Log("Expire Date     :" + s5);

                        datacs = FromHex(respCode);
                        s5 = Encoding.ASCII.GetString(datacs);
                        //Logger.Log("Response Code :" + getResponseCode(s5));

                        int z5 = 1;
                        while (true)
                        {
                            Console.WriteLine("Response Code :" + s5);

                            if (s5 != "TO")
                            {
                                Console.WriteLine("Last Response Code :" + s5 + " in " + z5 + " sec.");
                                break;//exit loop
                            }

                            Thread.Sleep(1000);
                            z5++;

                            if (z5 > 10)
                            {
                                break;
                            }


                        }

                        if (s5 == "00")
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.status = getResponseCode(s5);

                            if (isRequiredDb == "true")
                            {
                                //now post session to DB
                                connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                            }

                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.status = getResponseCode(s5);
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
                    else
                    {
                        if (dataResponse.Contains("APPROVED"))
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }

                    }

                    break;
                case "NETSQR":
                    if (ECR_TYPE == 2)
                    {
                        //get trx type
                        trxType = tokens[4] + "-" + tokens[5];

                        //get card pan
                        cardPan = tokens[30];
                        for (int y = 31; y <= 48; y++)
                        {
                            cardPan += "-" + tokens[y];
                        }

                        //get response code
                        respCode = tokens[754] + "-" + tokens[755];

                        //get RRN
                        RRN = tokens[152];
                        for (int y = 153; y <= 163; y++)
                        {
                            RRN += "-" + tokens[y];
                        }


                        //get auth code
                        authCode = tokens[141];
                        for (int y = 142; y <= 146; y++)
                        {
                            authCode += "-" + tokens[y];
                        }

                        //get trxDate
                        trxDate = tokens[75];
                        for (int y = 76; y <= 80; y++)
                        {
                            trxDate += "-" + tokens[y];
                        }

                        //get trxTime
                        trxTime = tokens[86];
                        for (int y = 87; y <= 91; y++)
                        {
                            trxTime += "-" + tokens[y];
                        }

                        //get merchant id
                        merchantId = tokens[110];
                        for (int y = 111; y <= 124; y++)
                        {
                            merchantId += "-" + tokens[y];
                        }

                        //get terminal id
                        terminalId = tokens[97];
                        for (int y = 98; y <= 104; y++)
                        {
                            terminalId += "-" + tokens[y];
                        }

                        //get Cardholder name
                        cardHolder = tokens[151];
                        for (int y = 152; y <= 170; y++)
                        {
                            cardHolder += "-" + tokens[y];
                        }

                        //get Ref id
                        stan = tokens[130];
                        for (int y = 131; y <= 135; y++)
                        {
                            stan += "-" + tokens[y];
                        }


                        //get issuer id
                        issuerId = tokens[165] + "-" + tokens[166];


                        transactionResponse.ID = session;


                        //render
                        byte[] dataqr = FromHex(trxType);
                        string s6 = Encoding.ASCII.GetString(dataqr);

                        transactionResponse.CardType = commandType;

                        dataqr = FromHex(cardPan);
                        s6 = Encoding.ASCII.GetString(dataqr);
                        transactionResponse.CardPAN = s6;

                        dataqr = FromHex(stan);
                        s6 = Encoding.ASCII.GetString(dataqr).Trim();
                        Logger.Log("STAN            :" + s6);

                        if (s6 != "")
                        {
                            var isNumeric = int.TryParse(s6, out int n);

                            if (isNumeric)
                            {
                                transactionResponse.STAN = long.Parse(s6);
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


                        dataqr = FromHex(authCode);
                        s6 = dataqr.Count() > 0 ? Encoding.ASCII.GetString(dataqr) : "0";
                        Logger.Log("Auth Code       :" + s6);

                        transactionResponse.AuthCode = s6;


                        dataqr = FromHex(merchantId);
                        s6 = Encoding.ASCII.GetString(dataqr);
                        Logger.Log("Merchant Id     :" + s6);
                        transactionResponse.MerchantId = s6;

                        dataqr = FromHex(terminalId);
                        s6 = Encoding.ASCII.GetString(dataqr);
                        Logger.Log("Terminal Id     :" + s6);
                        transactionResponse.TerminalID = s6;

                        dataqr = FromHex(RRN);
                        s6 = Encoding.ASCII.GetString(dataqr).Trim();
                        Logger.Log("RRN             :" + s6);
                        transactionResponse.TxnRef = s6;

                        dataqr = FromHex(cardHolder);
                        s6 = Encoding.ASCII.GetString(dataqr);

                        dataqr = FromHex(issuerId);
                        s6 = Encoding.ASCII.GetString(dataqr);
                        Logger.Log("Issuer Id       :" + getIssuerId(s6));
                        transactionResponse.CardType = commandType;

                        var dt6 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                        dataqr = FromHex(trxDate);
                        s6 = Encoding.ASCII.GetString(dataqr);

                        var a6 = s6.Substring(0, 2);
                        var b6 = s6.Substring(2, 2);
                        var c6 = s6.Substring(4, 2);
                        var DT6 = "20" + a6 + "-" + b6 + "-" + c6;
                        Logger.Log("Date            :" + DT6);

                        dataqr = FromHex(trxTime);
                        s6 = Encoding.ASCII.GetString(dataqr);
                        var d6 = s6.Substring(0, 2);
                        var e6 = s6.Substring(2, 2);
                        var f6 = s6.Substring(4, 2);
                        var ET6 = d6 + ":" + e6 + ":" + f6;
                        Logger.Log("Time            :" + ET6);

                        try
                        {
                            //set non TO
                            transactionResponse.BankDateTime = DateTime.Parse(DT6 + " " + ET6);

                        }
                        catch (Exception zx)
                        {

                        }
                        finally
                        {
                            transactionResponse.BankDateTime = DateTime.Parse(dt6);
                        }



                        dataqr = FromHex(respCode);
                        s6 = Encoding.ASCII.GetString(dataqr);
                        Logger.Log("Response Code :" + getResponseCode(s6));

                        int z6 = 1;
                        while (true)
                        {
                            Console.WriteLine("Response Code :" + s6);

                            if (s6 != "TO")
                            {
                                Console.WriteLine("Last Response Code :" + s6 + " in " + z6 + " sec.");
                                break;//exit loop
                            }

                            Thread.Sleep(1000);
                            z6++;

                            if (z6 > 10)
                            {
                                break;
                            }


                        }

                        if (s6 == "00")
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.status = getResponseCode(s6);

                            if (isRequiredDb == "true")
                            {
                                //now post session to DB
                                connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                            }

                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.status = getResponseCode(s6);
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
                    else
                    {
                        if (dataResponse.Contains("APPROVED"))
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.CardType = commandType;
                            transactionResponse.status = getResponseCode("00");
                        }

                    }

                    break;
                default:
                    isSucccessfull = false;
                    transactionResponse.result = "FAILED";
                    transactionResponse.status = getResponseCode("51");

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
                default :
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

                uniqueId = Get12Digits();
                //Logger.Log("Unique ID  : " + uniqueId);

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

        public string Get12Digits()
        {
            var bytes = new byte[4];
            var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            uint random = BitConverter.ToUInt32(bytes, 0) % 100000000;
            return String.Format("{0:D12}", random);
        }
        
    }
}

