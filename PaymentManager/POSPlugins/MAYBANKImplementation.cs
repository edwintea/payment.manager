using Newtonsoft.Json;
using PaymentManager;
using PaymentManager.DataContracts;
using PaymentManager.Utils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
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
    
    public class MAYBANKImplementation : PaymentInterface
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
        
        public static string MERCHANT_ID
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
                        0x02,0x02,0x56,0x38,0x39,0x32,0x30,0x31,0x37,0x30,0x35,0x32,0x30,0x31,0x32,0x35,0x38,0x35,0x35,0x30,0x30,0x30,0x30,0x30,0x31,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x03,0x58

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

        public Result DoTransaction(string merchant, string terminal, string orderNo, decimal amount, string commandType)
        {

            int bufferSize = 100000;
            
            string amountInString = ReformatPrice(amount);
            byte[] byteList = new byte[] { };
            payload = String.Empty;

            LogPayLoad(byteList);

            commandType = commandType.ToUpper();
            
            try
            {

                /*
                *02 02 56 30 31 30 30 30 30 30 30 30 31 32 30 30 30 32 30 31 37 30 35 32 30 31 32
                35 38 35 35 30 30 30 30 30 31 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 03 5B
                */

                if (ECR_TYPE == 1)
                {
                    byteList = new byte[] {
                        0x02,0x02,0x56,
                        0x30,0x31, //Trx Type
                        0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30, //amount
                        0x32,0x30,0x31,0x37,0x30,0x35,0x32,0x30,0x31,0x32,0x35,0x38,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,//order unique id
                        0x20,0x20,0x20,0x20,0x20,0x20,//Old Invoice NO.
                        0x20,0x20,0x20,0x20,0x20,0x20,//Old QRTrace NO.
                        0x20,0x20,//Settlement index
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20, //amount2
                    
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,

                        0x03,0x5B
                    };


                    Helper a = new Helper();
                    uniqueId = a.Get20Digits();

                    //CREATE ECN
                    int xc = 36; //transfered from uniqueid
                    foreach (char ch in uniqueId.Reverse())
                    {
                        byteList[xc] = Convert.ToByte(ch);

                        xc--;
                    }
                    
                }
                else if (ECR_TYPE == 2)
                {
                    
                    byteList = new byte[] {
                        0x02,0x02,0x56,
                        0x30,0x31, //Trx Type
                        0x32,0x30,0x31,0x37,0x30,0x35,0x32,0x30,0x31,0x32,0x35,0x38,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,//ECR TRANS NO
                        0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x30, //AMOUNT
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,// TIP AMOUNT
                       
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,

                        0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                        
                        0x03,0x5B
                    };

                    
                    Helper a = new Helper();
                    uniqueId = a.Get20Digits();
                    
                    //CREATE ECN
                    int xc = 24; //transfered from uniqueid
                    foreach (char ch in uniqueId.Reverse())
                    {
                        byteList[xc] = Convert.ToByte(ch);

                        xc--;
                    }
                    
                }

                //DEFAULT VALUE IN MAYBANKDB
                if (commandType == "MAYBANKQR")
                {
                    byteList[3] = 0x31; //Trx Type
                    byteList[4] = 0x30;
                }

                //SET AMOUNT
                int i = ECR_TYPE ==1?16:36;
                
                foreach (char ch in amountInString.Reverse())
                {
                    byteList[i] = Convert.ToByte(ch);
                    i--;
                }
                
                
                //collect LRC
                byte[] LRC = new byte[byteList.Length];
                int y = 0;
                for (var g = 0; g < (byteList.Length - 1); g++)
                {
                    LRC[y] = Convert.ToByte(byteList[g]);

                    y++;
                }
                
                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {
                    payLoadInStr.Append(b.ToString("x2") + " ");
                }
                
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
                    var stream = posClient.GetStream();
                    var readStream = new MemoryStream();

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
                                
                                binaryWriter.Write(byteList);//send the payload

                                Logger.Log("Sent. Waiting for the response...");
                                
                                try
                                {
                                    
                                    stream.ReadTimeout = 120000;
                                    
                                    int readBytes = stream.Read(byteList, 0, byteList.Length);
                                    
                                    while (true)
                                    {
                                        if(readBytes > 0)
                                        {
                                            readStream.Write(byteList, 0, readBytes);

                                            readBytes = stream.Read(byteList, 0, byteList.Length);

                                            break;
                                        }

                                        Console.WriteLine("Calculate Response ...");
                                        Thread.Sleep(1000);
                                        
                                    }

                                    var responseData = String.Empty;

                                    int yu = 0;
                                    while (true)
                                    {
                                        
                                        responseData = Encoding.ASCII.GetString(readStream.ToArray());
                                        
                                        if (responseData != null || responseData != "")
                                        {
                                            
                                            ProcessStringResponse(responseData, null, commandType);

                                            return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };
                                            break;
                                       
                                        }

                                        
                                        if (yu > 5)
                                        {
                                            return new Result() { result = isSucccessfull, message = "Terminal Connection Succesfully", data = transactionResponse };
                                            break;
                                        }

                                        Console.WriteLine("Waiting for response ...");
                                        Thread.Sleep(1000);
                                        yu++;
                                        
                                    }
                                    
                                }
                                catch (Exception t)
                                {
                                    Console.WriteLine(t.Message);
                                    
                                }

                                
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error Reader : " + ex.Message.ToString());

                        try
                        {
                            if (stream != null)
                            {
                                stream.Close();
                                Console.WriteLine("Binary Writer clear!");
                            }
                            else
                            {
                                stream.Close();
                                Console.WriteLine("Binary Writer clear!");
                            }
                            
                        }
                        catch (Exception ex1)
                        {
                            Logger.Log("" + ex1);
                        }

                        try
                        {
                            if (readStream != null)
                            {
                                readStream.Close();
                                Console.WriteLine("Binary Reader clear!");
                            }
                            else
                            {
                                readStream.Close();
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

            string amountInString = ReformatPrice(amount);
            byte[] byteList = new byte[] { };
            payload = String.Empty;
            

            try
            {
                /*
                *02 02 56 30 31 30 30 30 30 30 30 30 31 32 30 30 30 32 30 31 37 30 35 32 30 31 32
                35 38 35 35 30 30 30 30 30 31 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20
                20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 20 03 5B
                */
                
                byteList = new byte[] {
                    0x02,//stx
                    0x02,0x56,

                    0x39,0x30,//trx type
                    0x30,0x30,0x30,0x30,0x30,0x30,0x30,0x31,0x32,0x30,0x30,0x30,//amount
                    0x32,0x30,0x31,0x37,0x30,0x35,0x32,0x30,0x31,0x32,0x35,0x38,0x35,0x35,0x30,0x30,0x30,0x30,0x30,0x31,//ECR unique Order ID 
                    0x20,0x20,0x20,0x20,0x20,0x20,//Old Invoice NO.
                    0x20,0x20,0x20,0x20,0x20,0x20,//Old QRTrace NO.
                    0x20,0x20,//Settlement index
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,//Amount2

                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,0x20,
                    0x20,0x20,0x20,0x20,0x20,0x20, //reserved

                    0x03,//etx
                    0x5B//lrc
                };

                
                Helper a = new Helper();
                uniqueId = a.Get20Digits();

                //CREATE ECN
                int xc = 36; //transfered from uniqueid
                foreach (char ch in uniqueId.Reverse())
                {
                    byteList[xc] = Convert.ToByte(ch);

                    xc--;
                }


                //collect LRC
                byte[] LRC = new byte[byteList.Length];
                int y = 0;
                for (var g = 0; g < (byteList.Length - 1); g++)
                {
                    LRC[y] = Convert.ToByte(byteList[g]);

                    y++;
                }


                StringBuilder payLoadInStr = new StringBuilder();
                foreach (byte b in LRC)
                {
                    payLoadInStr.Append(b.ToString("x2") + " ");
                }

                Logger.Log("Item of LRC :\n" + payLoadInStr.ToString());

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

                            var stream = posClient.GetStream();
                            stream.ReadTimeout = 120000;

                            var readStream = new MemoryStream();
                            int readBytes = stream.Read(byteList, 0, byteList.Length);
                            while (readBytes > 0)
                            {
                                readStream.Write(byteList, 0, readBytes);

                                readBytes = stream.Read(byteList, 0, byteList.Length);
                            }
                            var responseData = String.Empty;

                            while (true)
                            {
                                responseData = Encoding.ASCII.GetString(readStream.ToArray());
                                if (responseData != null)
                                {
                                    ProcessStringResponse(responseData, null, commandType);
                                    Console.WriteLine("Response In String : " + responseData);
                                    return new Result() { result = true, message = "Terminal Connection Succesfully", data = transactionResponse };
                                    break;
                                }

                                Console.WriteLine("Waiting for response ...");
                                Thread.Sleep(1000);

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
                        try
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
                        catch(Exception rt)
                        {
                            Console.WriteLine("Failed to get Response!");
                            Console.WriteLine(rt.Message.ToString());
                        }
                        

                    }
                    else
                        pieceOfData = string.Empty;

                    var currentTimestamp = DateTime.Now;
                    var diffInSeconds = (currentTimestamp - sendingTime).TotalSeconds;

                    Console.WriteLine("Waiting response ....");


                    if (pieceOfData != null && pieceOfData.Length > 0)
                    {
                        
                        ProcessStringResponse(pieceOfData, session, commandType);

                        Thread.Sleep(1000);

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
                    else
                    {
                       
                    }

                    
                    Thread.Sleep(1000);

                    iCount++;

                    if(iCount > 10)
                    {
                        return ProcessResponse(finalResposne);
                    }

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
            String cardType = string.Empty;
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

            
            if (ECR_TYPE == 1)
            {
                
                switch (commandType)
                {
                    case "SETTLEMENT":

                        //get trx type
                        trxType = tokens[3] + "-" + tokens[4];

                        //get response code
                        respCode = tokens[5] + "-" + tokens[6];

                        //get card pan
                        cardPan = tokens[55];
                        for (int y = 56; y <= 58; y++)
                        {
                            cardPan += "-" + tokens[y];
                        }

                        //get card type
                        cardType = tokens[59];
                        for (int y = 60; y <= 68; y++)
                        {
                            cardType += "-" + tokens[y];
                        }

                        //get RRN
                        RRN = tokens[123];
                        for (int y = 124; y <= 134; y++)
                        {
                            RRN += "-" + tokens[y];
                        }


                        //get auth code
                        authCode = tokens[117];
                        for (int y = 118; y <= 122; y++)
                        {
                            authCode += "-" + tokens[y];
                        }

                        //get trxDate
                        trxDate = tokens[69];
                        for (int y = 70; y <= 78; y++)
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
                        terminalId = tokens[135];
                        for (int y = 136; y <= 142; y++)
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
                        //transactionResponse.CardType = s1;

                        data = FromHex(cardPan);
                        s1 = Encoding.ASCII.GetString(data);
                        transactionResponse.CardPAN = s1;

                        data = FromHex(cardType);
                        s1 = Encoding.ASCII.GetString(data);
                        transactionResponse.CardType = s1.Trim();

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
                        //transactionResponse.MerchantId = s1;
                        transactionResponse.MerchantId = MERCHANT_ID;


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
                        //transactionResponse.CardType = getIssuerId(s1);

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


                        if (dataResponse.Contains("SUCCESS"))
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.status = "SUCCESS";
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.status = "FAILED";
                        }

                        break;


                    default://SALE
                        try
                        {
                            //get trx type
                            trxType = tokens[3] + "-" + tokens[4];

                            //get response code
                            respCode = tokens[5] + "-" + tokens[6];

                            //get card pan
                            cardPan = tokens[55];
                            for (int y = 56; y <= 58; y++)
                            {
                                cardPan += "-" + tokens[y];
                            }

                            //get card type
                            cardType = tokens[59];
                            for (int y = 60; y <= 68; y++)
                            {
                                cardType += "-" + tokens[y];
                            }

                            //get RRN
                            RRN = tokens[123];
                            for (int y = 124; y <= 134; y++)
                            {
                                RRN += "-" + tokens[y];
                            }


                            //get auth code
                            authCode = tokens[117];
                            for (int y = 118; y <= 122; y++)
                            {
                                authCode += "-" + tokens[y];
                            }

                            //get trxDate
                            trxDate = tokens[69];
                            for (int y = 70; y <= 78; y++)
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
                            terminalId = tokens[135];
                            for (int y = 136; y <= 142; y++)
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
                            stan = tokens[111];
                            for (int y = 112; y <= 116; y++)
                            {
                                stan += "-" + tokens[y];
                            }


                            //get issuer id
                            issuerId = tokens[165] + "-" + tokens[166];
                            //transactionResponse.ID = session;


                            //render
                            byte[] data1 = FromHex(trxType);
                            string s2 = Encoding.ASCII.GetString(data1);

                            data1 = FromHex(cardPan);
                            s2 = Encoding.ASCII.GetString(data1);
                            transactionResponse.CardPAN = s2;

                            data1 = FromHex(cardType);
                            s2 = Encoding.ASCII.GetString(data1);
                            if (commandType == "MAYBANKDB")
                            {
                                transactionResponse.CardType = s2.Trim();

                            }
                            else
                            {
                                transactionResponse.CardType = "MAYBANKQR";

                            }

                            data1 = FromHex(stan);
                            s2 = Encoding.ASCII.GetString(data1).Trim();
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


                            data1 = FromHex(authCode);
                            s2 = data1.Count() > 0 ? Encoding.ASCII.GetString(data1) : "0";
                            Logger.Log("Auth Code       :" + s2);

                            transactionResponse.AuthCode = s2;


                            data1 = FromHex(merchantId);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Merchant Id     :" + s2);
                            transactionResponse.MerchantId = s2;
                            transactionResponse.MerchantId = MERCHANT_ID;

                            data1 = FromHex(terminalId);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Terminal Id     :" + s2);
                            transactionResponse.TerminalID = s2;

                            data1 = FromHex(RRN);
                            s2 = Encoding.ASCII.GetString(data1).Trim();
                            Logger.Log("RRN             :" + s2);
                            transactionResponse.TxnRef = s2;

                            data1 = FromHex(cardHolder);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Card Holder     :" + s2);

                            data1 = FromHex(issuerId);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Issuer Id       :" + getIssuerId(s2));
                            //transactionResponse.CardType = getIssuerId(s2);

                            var dt2 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                            data1 = FromHex(trxDate);
                            s2 = Encoding.ASCII.GetString(data1);

                            var a2 = s2.Substring(0, 2);
                            var b2 = s2.Substring(2, 2);
                            var c2 = s2.Substring(4, 2);
                            var DT2 = "20" + a2 + "-" + b2 + "-" + c2;
                            Logger.Log("Date            :" + DT2);

                            data1 = FromHex(trxTime);
                            s2 = Encoding.ASCII.GetString(data1);
                            var d2 = s2.Substring(0, 2);
                            var e2 = s2.Substring(2, 2);
                            var f2 = s2.Substring(4, 2);
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


                            data = FromHex(respCode);
                            s1 = Encoding.ASCII.GetString(data);
                            //Logger.Log("Response Code :" + getResponseCode(s1));

                            //Console.WriteLine("==============>" + transactionResponse.AuthCode);
                            if (commandType == "MAYBANKQR")
                            {
                                if (dataResponse.Contains("SUCCESS"))
                                {
                                    isSucccessfull = true;
                                    transactionResponse.result = "APPROVED";
                                    transactionResponse.status = "SUCCESS";
                                    //now post session to DB
                                    connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                                }
                                else
                                {
                                    isSucccessfull = false;
                                    transactionResponse.result = "FAILED";
                                    transactionResponse.status = "FAILED";
                                }
                            }


                            if (commandType == "MAYBANKDB")
                            {
                                if (s1 == "00" || Convert.ToInt32(transactionResponse.AuthCode) > 0)
                                {
                                    isSucccessfull = true;
                                    transactionResponse.result = "APPROVED";
                                    transactionResponse.status = "APPROVED";

                                    connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());

                                }
                                else
                                {
                                    isSucccessfull = false;
                                    transactionResponse.result = "FAILED";
                                    transactionResponse.status = getResponseCode(s2);

                                }
                            }


                        }
                        catch (Exception ty)
                        {
                            Console.WriteLine("Method is not allowed bro!");
                        }



                        break;
                }
            }
            else
            {
                //RESPONSE OF ECR 2

                switch (commandType)
                {
                    case "SETTLEMENT":

                        //get trx type
                        trxType = tokens[3] + "-" + tokens[4];

                        //get response code
                        respCode = tokens[5] + "-" + tokens[6];
                        

                        //get card pan
                        cardPan = tokens[55];
                        for (int y = 56; y <= 58; y++)
                        {
                            cardPan += "-" + tokens[y];
                        }

                        //get card type
                        cardType = tokens[59];
                        for (int y = 60; y <= 68; y++)
                        {
                            cardType += "-" + tokens[y];
                        }

                        //get RRN
                        RRN = tokens[123];
                        for (int y = 124; y <= 134; y++)
                        {
                            RRN += "-" + tokens[y];
                        }


                        //get auth code
                        authCode = tokens[117];
                        for (int y = 118; y <= 122; y++)
                        {
                            authCode += "-" + tokens[y];
                        }

                        //get trxDate
                        trxDate = tokens[69];
                        for (int y = 70; y <= 78; y++)
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
                        terminalId = tokens[135];
                        for (int y = 136; y <= 142; y++)
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
                        //transactionResponse.CardType = s1;
                        

                        data = FromHex(cardPan);
                        s1 = Encoding.ASCII.GetString(data);
                        transactionResponse.CardPAN = s1;

                        data = FromHex(cardType);
                        s1 = Encoding.ASCII.GetString(data);
                        transactionResponse.CardType = s1.Trim();

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
                        //transactionResponse.MerchantId = MERCHANT_ID;


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
                        //transactionResponse.CardType = getIssuerId(s1);

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


                        if (dataResponse.Contains("SUCCESS"))
                        {
                            isSucccessfull = true;
                            transactionResponse.result = "APPROVED";
                            transactionResponse.status = "SUCCESS";
                        }
                        else
                        {
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.status = "FAILED";
                        }

                        break;


                    default: //SALE
                        try
                        {

                            //get trx type
                            trxType = tokens[3] + "-" + tokens[4];

                            //get response code
                            respCode = tokens[5] + "-" + tokens[6];

                            byte[] resp = FromHex(respCode);
                            string respStr = Encoding.ASCII.GetString(resp);
                            

                            
                            //get card pan
                            cardPan = tokens[23];
                            for (int y = 24; y <= 26; y++)
                            {
                                cardPan += "-" + tokens[y];
                            }

                            //get card type
                            cardType = tokens[27];
                            for (int y = 28; y <= 36; y++)
                            {
                                cardType += "-" + tokens[y];
                            }

                            //get RRN
                            RRN = tokens[126];
                            for (int y = 127; y <= 137; y++)
                            {
                                RRN += "-" + tokens[y];
                            }


                            //get auth code
                            authCode = tokens[138];
                            for (int y = 139; y <= 143; y++)
                            {
                                authCode += "-" + tokens[y];
                            }

                            //get trxDate
                            trxDate = tokens[69];
                            for (int y = 70; y <= 78; y++)
                            {
                                trxDate += "-" + tokens[y];
                            }

                            //get trxTime
                            trxTime = tokens[80];
                            for (int y = 81; y <= 89; y++)
                            {
                                trxTime += "-" + tokens[y];
                            }

                            //get merchant id
                            merchantId = tokens[45];
                            for (int y = 46; y <= 59; y++)
                            {
                                merchantId += "-" + tokens[y];
                            }

                            //get terminal id
                            terminalId = tokens[37];
                            for (int y = 38; y <= 44; y++)
                            {
                                terminalId += "-" + tokens[y];
                            }

                            //get Cardholder name
                            cardHolder = tokens[148];
                            for (int y = 149; y <= 164; y++)
                            {
                                cardHolder += "-" + tokens[y];
                            }

                            //get Ref id
                            stan = tokens[111];
                            for (int y = 112; y <= 116; y++)
                            {
                                stan += "-" + tokens[y];
                            }


                            //get issuer id
                            issuerId = tokens[165] + "-" + tokens[166];
                            //transactionResponse.ID = session;


                            //render response
                            byte[] data1 = FromHex(trxType);
                            string s2 = Encoding.ASCII.GetString(data1);

                            //******** ecr no *********/
                            var ecrNo = tokens[60];
                            for (int y = 61; y <= 79; y++)
                            {
                                ecrNo += "-" + tokens[y];
                            }

                            data = FromHex(ecrNo);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("ECR TRX NO       :" + s2);
                            transactionResponse.ecrTrxNo = s2;
                            //******** end ecr no *********/


                            //******** invoice no *********/
                            invoiceNo = tokens[114];
                            for (int y = 115; y <= 119; y++)
                            {
                                invoiceNo += "-" + tokens[y];
                            }

                            data = FromHex(invoiceNo);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("INVOICE NO       :" + s2);
                            transactionResponse.invoiceNo = s2;
                            //******** end invoice no *********/


                            //******** batch no *********/
                            var batchNo = tokens[120];
                            for (int y = 121; y <= 125; y++)
                            {
                                batchNo += "-" + tokens[y];
                            }

                            data = FromHex(batchNo);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("BATCH NO        :" + s2);
                            transactionResponse.batchNo = s2;
                            //******** end batch no *********/


                            //******** expire date *********/
                            var expire = tokens[144];
                            for (int y = 145; y <= 148; y++)
                            {
                                expire += "-" + tokens[y];
                            }

                            data = FromHex(expire);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("EXPIRE DATE     :" + s2);
                            transactionResponse.expireDate = s2.Trim();
                            //******** end expire date *********/


                            //******** app *********/
                            var app = tokens[148];
                            for (int y = 149; y <= 164; y++)
                            {
                                app += "-" + tokens[y];
                            }

                            data = FromHex(app);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("APP            :" + s2);
                            transactionResponse.APP = s2.Trim();
                            //******** end APP no *********/

                            //******** AID *********/
                            var aid = tokens[165];
                            for (int y = 166; y <= 196; y++)
                            {
                                aid += "-" + tokens[y];
                            }

                            data = FromHex(aid);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("AID           :" + s2);
                            transactionResponse.AID = s2.Trim();
                            //******** end AID no *********/

                            //******** TC *********/
                            var tc = tokens[197];
                            for (int y = 198; y <= 212; y++)
                            {
                                tc += "-" + tokens[y];
                            }

                            data = FromHex(tc);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("TC           :" + s2);
                            transactionResponse.TC = s2.Trim();
                            //******** end TC no *********/

                            //******** TVR *********/
                            var tvr = tokens[213];
                            for (int y = 214; y <= 222; y++)
                            {
                                tvr += "-" + tokens[y];
                            }

                            data = FromHex(tvr);
                            s2 = Encoding.ASCII.GetString(data);
                            Logger.Log("TVR          :" + s2);
                            transactionResponse.TVR = s2.Trim();
                            //******** end TVR no *********/



                            data1 = FromHex(cardPan);
                            s2 = Encoding.ASCII.GetString(data1);
                            transactionResponse.CardPAN = "****************"+s2;

                            data1 = FromHex(cardType);
                            s2 = Encoding.ASCII.GetString(data1);

                            if (commandType == "MAYBANKDB" || String.IsNullOrEmpty(commandType))
                            {
                                transactionResponse.CardType = s2.Trim();

                            }
                            else
                            {
                                transactionResponse.CardType = "MAYBANKQR";

                            }

                            data1 = FromHex(stan);
                            s2 = Encoding.ASCII.GetString(data1).Trim();
                            Logger.Log("STAN            :" + s2);
                            if(s2 != "")
                            {
                                transactionResponse.STAN = long.Parse(s2);
                            }
                            else
                            {
                                transactionResponse.STAN =0;
                            }


                            data1 = FromHex(authCode);
                            s2 = data1.Count() > 0 ? Encoding.ASCII.GetString(data1) : "0";
                            Logger.Log("Auth Code       :" + s2);
                            transactionResponse.AuthCode = s2.Trim();
                            

                            data1 = FromHex(merchantId);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Merchant Id     :" + s2);
                            transactionResponse.MerchantId = s2.Trim();
                            
                            data1 = FromHex(terminalId);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Terminal Id     :" + s2);
                            transactionResponse.TerminalID = s2.Trim();

                            data1 = FromHex(RRN);
                            s2 = Encoding.ASCII.GetString(data1).Trim();
                            Logger.Log("RRN             :" + s2);
                            transactionResponse.TxnRef = s2.Trim();

                            data1 = FromHex(cardHolder);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Card Holder     :" + s2);

                            data1 = FromHex(issuerId);
                            s2 = Encoding.ASCII.GetString(data1);
                            Logger.Log("Issuer Id       :" + getIssuerId(s2));
                            
                            var dt2 = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                            data1 = FromHex(trxDate);
                            s2 = Encoding.ASCII.GetString(data1);

                            var a2 = s2.Substring(0, 2);
                            var b2 = s2.Substring(2, 2);
                            var c2 = s2.Substring(4, 2);
                            var DT2 = "20" + a2 + "-" + b2 + "-" + c2;
                            Logger.Log("Date            :" + DT2);

                            data1 = FromHex(trxTime);
                            s2 = Encoding.ASCII.GetString(data1);
                            var d2 = s2.Substring(0, 2);
                            var e2 = s2.Substring(2, 2);
                            var f2 = s2.Substring(4, 2);
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


                            data = FromHex(respCode);
                            s1 = Encoding.ASCII.GetString(data);
                            Logger.Log("Response Code :" + respStr);
                            
                            if (respStr == "00")
                            {
                                isSucccessfull = true;
                                transactionResponse.result = "APPROVED";
                                transactionResponse.status = "APPROVED";
                                
                                connect.InsertPayment(session, transactionResponse.TerminalID, 1, payload, byteString, transactionResponse.ToJsonString());


                            }
                            else
                            {
                                if (ResponseText.Contains("QUIT"))
                                {
                                    transactionResponse.status = "PAYMENT WAS CANCELLED";
                                }

                                if (ResponseText.Contains("FAIL"))
                                {
                                    transactionResponse.status = "PAYMENT WAS DECLINED";
                                }
                                
                                isSucccessfull = false;
                                transactionResponse.result = "FAILED";
                                transactionResponse.status = getResponseCode(respStr);

                            }
                            
                        }
                        catch (Exception ex)
                        {
                            
                            Console.WriteLine(ex.StackTrace + " ---This is your line number, bro' :)", ex.Message);
                            isSucccessfull = false;
                            transactionResponse.result = "FAILED";
                            transactionResponse.status = "PAYMENT WAS DECLINED";
                        }



                        break;
                }

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
                case "ER":
                    h = "QUIT TRANSACTION";
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

