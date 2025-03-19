using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PaymentManager.DataContracts;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tabsquare.Payment;
using PaymentManager.Utils;
using log4net;
using System.Reflection;
using System.Net.NetworkInformation;
using System.Collections;

namespace PaymentManager
{
    
    class SocketManager
    {

        static TcpListener listener;
        private static SocketPermission _permission;
        private static Socket _sListener;
        private static IPEndPoint _ipEndPoint;
        private static readonly ASCIIEncoding EncodingAscii = new ASCIIEncoding();
        private static string _receiveMessage = string.Empty;
        private static Socket _listener;
        private static bool _connect = true;
        public static TransactionResponse transactionResponse;
        public static Queue qt = new Queue();
        public static bool IS_DEBUG = ConfigurationManager.AppSettings["DEBUG"] == "2606";
        public static log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(Program));

        public static string dataToEdc = String.Empty;

        private static DBConnect connect = new DBConnect();
        public static string isRequiredDb = ConfigurationSettings.AppSettings["DB_REQUIRED"].ToLower();
        public static string PAYMENT_VENDOR = ConfigurationSettings.AppSettings["PAYMENT_VENDOR"].ToUpper();
        static readonly JsonSerializer _serializer = new JsonSerializer();
        
        static ConsoleSpinner spinner = new ConsoleSpinner();

        public static Socket socketReceive = null;
        public static int counter;
        
        public static long rrn = 0;

        public static List<Socket> _clientSocket = new List<Socket>();
        public static byte[] buffer= new byte[1024 * 1024];

        public static bool PortInUse(int port)
        {
            bool inUse = false;

            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] ipEndPoints = ipProperties.GetActiveTcpListeners();


            foreach (IPEndPoint endPoint in ipEndPoints)
            {
                if (endPoint.Port == port)
                {
                    inUse = true;
                    break;
                }
            }


            return inUse;
        }

        public static void InitServerSocket()
        {
            try
            {

                var ipAddress = ConfigurationSettings.AppSettings["LISTENING_IP"];

                var port = ConfigurationSettings.AppSettings["LISTENING_PORT"];

                Logger.Log("***************************************************************");

                Logger.Log("Terminal           : "+ ConfigurationSettings.AppSettings["PAYMENT_VENDOR"].ToUpper());
                Logger.Log("ECR Type           : " + ConfigurationSettings.AppSettings["ECR_TYPE"].ToUpper());

                Logger.Log("IP Address         : " + ipAddress + ":" + port);

                Logger.Log("Merchant           : " + ConfigurationSettings.AppSettings["PROJECT_NAME"].ToUpper());

                Logger.Log("***************************************************************");

                // Creates one SocketPermission object for access restrictions
                _permission = new SocketPermission(NetworkAccess.Accept, TransportType.Tcp, "", SocketPermission.AllPorts);

                // Listening Socket object 
                _sListener = null;

                // Ensures the code to have permission to access a Socket 
                _permission.Demand();

                // Creates a network endpoint 
                //_ipEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), Convert.ToInt32(port));

                _ipEndPoint = new IPEndPoint(IPAddress.Any, Convert.ToInt32(port));
                
                // Create one Socket object to listen the incoming connection 
                _sListener = new Socket(
                    IPAddress.Parse(ipAddress).AddressFamily,
                    SocketType.Stream,
                    ProtocolType.Tcp
                    );
                
                _sListener.ReceiveTimeout = 1;
                _sListener.SendTimeout = 1;


                // Associates a Socket with a local endpoint 
                _sListener.Bind(_ipEndPoint);
                _sListener.Listen(30000);
                
                _connect = true;
                _logger.Info("Terminal Connected.");
                

                Thread thread = new Thread(new ThreadStart(WorkThreadFunction));
                qt.Enqueue(thread);
                thread.Start();


            }
            catch (Exception ex)
            {
                Logger.Log(ex);
                return;
            }
        }

        private static void WorkThreadFunction()
        {
            try
            {

                var aCallback = new AsyncCallback(AcceptCallback);

                _sListener.BeginAccept(aCallback, _sListener);

            }
            catch
            {
                return;
            }
        }

        private static void SendEDC()
        {
            try
            {

                socketReceive.Send(Encoding.UTF8.GetBytes(dataToEdc));
                

            }
            catch
            {
                return;
            }
        }

        

        private static void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                
                // A new Socket to handle remote host communication 
                if (!_connect)
                {
                    return;
                }
                // Receiving byte array 
                
                // Get Listening Socket object 
                _listener = (Socket)ar.AsyncState;
                // Create a new socket 
                Socket endAccept = _listener.EndAccept(ar);
                // Using the Nagle algorithm 
                endAccept.NoDelay = false;

                //add by edwin
                _clientSocket.Add(endAccept);
                
                // Creates one object array for passing data 
                var obj = new object[2];
                obj[0] = buffer;
                obj[1] = endAccept;

                // Begins to asynchronously receive data 

                qt.Enqueue(endAccept.BeginReceive(
                    buffer,
                    0,
                    buffer.Length,
                    SocketFlags.None,
                    ReceiveCallback,
                    obj
                    ));

                // Begins an asynchronous operation to accept an attempt 
                var aCallback = new AsyncCallback(AcceptCallback);
                _listener.BeginAccept(aCallback, _listener);

                //Console.WriteLine("Accept Callback ");
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }

        public static void ReceiveCallback(IAsyncResult ar)
        {
            if (IS_DEBUG)
                Logger.Log("Begin receiving callback ReceiveCallback()");
                //Console.WriteLine("Begin receiving callback ReceiveCallback()");

            //Fetch a user-defined object that contains information 
            var obj = (object[])ar.AsyncState;

            // Received byte array 
            var buffer = (byte[])obj[0];

            // A Socket to handle remote host communication. 
            //var socketReceive = (Socket)obj[1];

            
            socketReceive = (Socket)obj[1];
            
            // The number of bytes received. 
            int bytesRead = socketReceive.EndReceive(ar);

            byte[] dataBuf = new byte[bytesRead];
            Array.Copy(buffer, dataBuf, bytesRead);

            if (bytesRead <= 0)
            {
                return;
            }

            _receiveMessage = EncodingAscii.GetString(buffer, 0, bytesRead);
            //Console.WriteLine("Message : " + _receiveMessage);


            if (IS_DEBUG)
                Logger.Log("Finish initing SocketHelper");

            
            string response = ProcessMsg(_receiveMessage);

            try
            {
                dataToEdc = response;
                //Thread thread = new Thread(new ThreadStart(SendEDC));
                //thread.Start();

                socketReceive.Send(Encoding.UTF8.GetBytes(dataToEdc));
                socketReceive.Shutdown(SocketShutdown.Both);

                socketReceive.Close();
                qt.Dequeue();
                
            }
            catch (Exception e)
            {
                
                //socketReceive.Shutdown(SocketShutdown.Both);
                //socketReceive.Close();

            }
            finally
            {
                //socketReceive.Shutdown(SocketShutdown.Both);
                //socketReceive.Close();
            }


            if (IS_DEBUG) Logger.Log("Finish receiving callback ReceiveCallback() ");
        }


        public static string ProcessMsg(string message)
        {

            //// Handle the message received and send a response back to the client.
            //_caughtException = false;                                                  

            Result response = FuncNavigator(message);

            string _mstrResponse = response.ToJsonString();

            return _mstrResponse;
        }

        private static Result FuncNavigator(string parsedData)
        {
            var response = new Result();
            response.result = true;

            Logger.Log("Requested {0}" + parsedData);

            try
            {
                
                JObject dictData = JObject.Parse(parsedData);

                string functionName = dictData["function"].ToString().ToLower();
                string payment_type = dictData["payment_type"].ToString().ToUpper();


                string tableName = string.Empty;

                string merchant = string.Empty;
                string acquirer_bank = string.Empty;
                string terminal = string.Empty;
                string session = string.Empty;
                decimal amount = 0.0M;
                
                Result result = null;

                switch (functionName)
                {
                    case "logon":

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));
                        
                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");


                        result = POSManager.Instance.DoLogOn(merchant, terminal);
                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");


                        return result;

                        break;

                    case "tms":

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));

                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");


                        result = POSManager.Instance.DoTMS(merchant, terminal);
                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");


                        return result;

                        break;

                    case "terminalping":

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));

                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");


                        result = POSManager.Instance.GetTerminalStatus(merchant, terminal);
                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");


                        return result;

                        break;


                    case "transaction":

                        
                        if (dictData["amount"] != null)
                            amount = Decimal.Parse(dictData["amount"].ToString());
                        else
                            throw new Exception(String.Format("Missing parameter 'amount'!!!"));

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));

                        session = string.Empty;
                        if (dictData["session"] != null)
                            session = dictData["session"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'session'!!!"));

                        string orderNo = string.Empty;
                        if (dictData["orderNo"] != null)
                            orderNo = dictData["orderNo"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'orderNo'!!!"));

                        if (dictData["payment_type"] != null)
                            payment_type = dictData["payment_type"].ToString().ToLower();
                        else
                            throw new Exception(String.Format("Missing parameter 'payment_type'!!!"));

                        string txn_date_time = string.Empty;
                        if (dictData["txn_date_time"] != null)
                            txn_date_time = dictData["txn_date_time"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'txn_date_time'!!!"));

                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("Session       : " + session);
                        Logger.Log("OrderNo       : " + orderNo);
                        Logger.Log("Amount        : " + amount);
                        Logger.Log("Payment Type  : " + payment_type.ToUpper());
                        Logger.Log("Date          : " + txn_date_time);
                        Logger.Log("Request       :" + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");

                        //check is session has in DB?

                        if (isRequiredDb == "true")
                        {
                            int count = connect.CountPaymentBySession(session);
                            Logger.Log("Session on DB       :" + count);

                            if (count > 0)
                            {
                                Logger.Log("Here 1");

                                if (connect.CloseConnection())
                                {
                                    connect.OpenConnection();
                                }

                                //get data from DB with this session
                                var getPayments = new List<string>[1];
                                getPayments = connect.getPaymentBySession(session);

                                
                                if (getPayments[0].Count() > 0)
                                {
                                    
                                    foreach (var a in getPayments[0])
                                    {
                                        
                                        dynamic lastPayments = JsonConvert.DeserializeObject(a);
                                        TransactionResponse transactionResponse = new TransactionResponse();

                                        transactionResponse.STAN = lastPayments["STAN"];
                                        transactionResponse.MerchantId = lastPayments["MerchantId"];
                                        transactionResponse.TerminalID = lastPayments["TerminalID"];
                                        transactionResponse.BankDateTime = lastPayments["BankDateTime"];
                                        transactionResponse.TxnRef = lastPayments["TxnRef"];
                                        transactionResponse.CardPAN = lastPayments["CardPAN"];
                                        transactionResponse.Receipt = lastPayments["Receipt"];
                                        transactionResponse.CardType = lastPayments["CardType"];
                                        transactionResponse.AuthCode = lastPayments["AuthCode"];
                                        transactionResponse.status = lastPayments["status"];
                                        transactionResponse.ID = lastPayments["ID"];
                                        transactionResponse.result = lastPayments["result"];
                                        transactionResponse.surcharge = lastPayments["surcharge"];

                                        result= new Result()
                                        {
                                            result = true,
                                            message = "The transaction settled successfully!",
                                            data = transactionResponse
                                        };


                                    }
                                    
                                }
                                else
                                {
                                    return new Result()
                                    {
                                        result = false,
                                        message = "The transaction settled successfully!",
                                        data = transactionResponse
                                    };

                                }
                            }
                            else
                            {
                                Logger.Log("Here 2");
                                Logger.Log("Payment Type :" + payment_type);

                                if (payment_type != "sakuku")
                                {
                                    Logger.Log("Response was from terminal");
                                    result = POSManager.Instance.DoTransaction(merchant, terminal, session, amount, payment_type);

                                }
                                else
                                {
                                    result = POSManager.Instance.DoTransaction(merchant, terminal, session, amount, payment_type);

                                    if (!result.result) // if command 26 done (print qr)continue to 28
                                    {
                                        try
                                        {
                                            JObject y = JObject.Parse(result.data.ToJSON().ToString());
                                            rrn = long.Parse(y["TxnRef"].ToString());

                                            //result = POSManager.Instance.DoInquirySakuku(amount, rrn);
                                            //Thread.Sleep(3000);

                                        }
                                        catch
                                        {
                                            result = new Result() { result = false, message = "Transaksi Gagal.Silahkan hubungi staff untuk bantuan.", data = transactionResponse };
                                        }


                                    }

                                }


                            }

                        }
                        else
                        {
                            result = POSManager.Instance.DoTransaction(merchant, terminal, session, amount, payment_type);
                        }

                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");

                        return result;

                        break;

                    case "inquiry":

                        amount = 0.0M;
                        if (dictData["amount"] != null)
                            amount = Decimal.Parse(dictData["amount"].ToString());
                        else
                            throw new Exception(String.Format("Missing parameter 'amount'!!!"));

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));

                        session = string.Empty;
                        if (dictData["session"] != null)
                            session = dictData["session"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'session'!!!"));

                        orderNo = string.Empty;
                        if (dictData["orderNo"] != null)
                            orderNo = dictData["orderNo"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'orderNo'!!!"));

                        payment_type = string.Empty;
                        if (dictData["payment_type"] != null)
                            payment_type = dictData["payment_type"].ToString().ToLower();
                        else
                            throw new Exception(String.Format("Missing parameter 'payment_type'!!!"));

                        txn_date_time = string.Empty;
                        if (dictData["txn_date_time"] != null)
                            txn_date_time = dictData["txn_date_time"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'txn_date_time'!!!"));
                        
                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("Session       : " + session);
                        Logger.Log("RRN           : " + orderNo);
                        Logger.Log("Amount        : " + amount);
                        Logger.Log("Payment Type  : " + payment_type.ToUpper());
                        Logger.Log("Date          : " + txn_date_time);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");

                        //result = POSManager.Instance.DoInquirySakuku(amount, long.Parse(orderNo));
                        result = POSManager.Instance.DoContinueSakuku(amount, long.Parse(orderNo));

                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");

                        return result;

                        break;


                    case "settlement":

                        if (dictData["acquirer_bank"] != null)
                            acquirer_bank = dictData["acquirer_bank"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'acquirer_bank'!!!"));

                        if (dictData["amount"] != null)
                            amount = Decimal.Parse(dictData["amount"].ToString());
                        else
                            throw new Exception(String.Format("Missing parameter 'amount'!!!"));

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));
                        
                        if (dictData["orderNo"] != null)
                            orderNo = dictData["orderNo"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'orderNo'!!!"));

                        if (dictData["payment_type"] != null)
                            payment_type = dictData["payment_type"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'payment_type'!!!"));

                        if (dictData["txn_date_time"] != null)
                            txn_date_time = dictData["txn_date_time"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'txn_date_time'!!!"));

                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Acquirer_bank : " + acquirer_bank);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("OrderNo       : " + orderNo);
                        Logger.Log("Amount        : " + amount);
                        Logger.Log("Payment Type  : " + payment_type.ToUpper());
                        Logger.Log("Date          : " + txn_date_time);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");

                        if (PAYMENT_VENDOR == "NETTS")
                        {
                            result = POSManager.Instance.DoSettlement(merchant, terminal, orderNo, amount, "settlement", acquirer_bank.ToUpper());
                            result = POSManager.Instance.DoLogOn(merchant, terminal);

                        }
                        else
                        {
                            result = POSManager.Instance.DoSettlement(merchant, terminal, orderNo, amount, "settlement", acquirer_bank.ToUpper());

                        }

                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");


                        return result;

                        break;
                    case "lasttransaction":
                        amount = 0.0M;
                        if (dictData["amount"] != null)
                            amount = Decimal.Parse(dictData["amount"].ToString());
                        else
                            throw new Exception(String.Format("Missing parameter 'amount'!!!"));

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));

                        session = string.Empty;
                        if (dictData["session"] != null)
                            session = dictData["session"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'session'!!!"));

                        orderNo = string.Empty;
                        if (dictData["orderNo"] != null)
                            orderNo = dictData["orderNo"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'orderNo'!!!"));

                        payment_type = string.Empty;
                        if (dictData["payment_type"] != null)
                            payment_type = dictData["payment_type"].ToString().ToLower();
                        else
                            throw new Exception(String.Format("Missing parameter 'payment_type'!!!"));

                        txn_date_time = string.Empty;
                        if (dictData["txn_date_time"] != null)
                            txn_date_time = dictData["txn_date_time"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'txn_date_time'!!!"));
                        
                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("Session       : " + session);
                        Logger.Log("RRN           : " + orderNo);
                        Logger.Log("Amount        : " + amount);
                        Logger.Log("Payment Type  : " + payment_type.ToUpper());
                        Logger.Log("Date          : " + txn_date_time);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");

                        if (isRequiredDb == "true")
                        {
                            int count = connect.CountPaymentBySession(session);
                            
                            if (connect.CloseConnection())
                            {
                                connect.OpenConnection();
                            }

                            //get data from DB with this session
                            var getPayments = new List<string>[1];
                            
                            getPayments = connect.getPaymentBySession(session);

                            if (getPayments[0].Count() > 0)
                            {
                                
                                foreach (var a in getPayments[0])
                                {
                                    dynamic lastPayments = JsonConvert.DeserializeObject(a);
                                    TransactionResponse transactionResponse = new TransactionResponse();

                                    transactionResponse.STAN = lastPayments["STAN"];
                                    transactionResponse.MerchantId = lastPayments["MerchantId"];
                                    transactionResponse.TerminalID = lastPayments["TerminalID"];
                                    transactionResponse.BankDateTime = lastPayments["BankDateTime"];
                                    transactionResponse.TxnRef = lastPayments["TxnRef"];
                                    transactionResponse.CardPAN = lastPayments["CardPAN"];
                                    transactionResponse.Receipt = lastPayments["Receipt"];
                                    transactionResponse.CardType = lastPayments["CardType"];
                                    transactionResponse.AuthCode = lastPayments["AuthCode"];
                                    transactionResponse.status = lastPayments["status"];
                                    transactionResponse.ID = lastPayments["ID"];
                                    transactionResponse.result = lastPayments["result"];
                                    transactionResponse.surcharge = lastPayments["surcharge"];

                                    Logger.Log("Response      : " + Logger.SerializeObject(new Result()
                                    {
                                        result = true,
                                        message = "The transaction settled successfully!",
                                        data = transactionResponse
                                    }));

                                    return new Result()
                                    {
                                        result = true,
                                        message = "The transaction settled successfully!",
                                        data = transactionResponse
                                    };
                                        
                                }

                            }
                            else
                            {
                                Logger.Log("***************************************************************");
                                Logger.Log("Response      : \n" + Logger.SerializeObject(new Result() { result = false, message = "No Last Transaction", data = null }));
                                Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");

                                return new Result() { result = false, message = "No Last Transaction", data = null };

                            }

                        }
                        else
                        {
                            result = POSManager.Instance.GetLastTransaction();
                            Logger.Log("***************************************************************");
                            Logger.Log("Response         : \n" + Logger.SerializeObject(result));
                            Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");

                            return result;

                        }
                        
                        break;
                    case "lastapprovedtransaction":

                        if (dictData["amount"] != null)
                            amount = Decimal.Parse(dictData["amount"].ToString());
                        else
                            throw new Exception(String.Format("Missing parameter 'amount'!!!"));

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));

                        orderNo = string.Empty;
                        if (dictData["orderNo"] != null)
                            orderNo = dictData["orderNo"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'orderNo'!!!"));

                        payment_type = string.Empty;
                        if (dictData["payment_type"] != null)
                            payment_type = dictData["payment_type"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'payment_type'!!!"));

                        txn_date_time = string.Empty;
                        if (dictData["txn_date_time"] != null)
                            txn_date_time = dictData["txn_date_time"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'txn_date_time'!!!"));

                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("OrderNo       : " + orderNo);
                        Logger.Log("Amount        : " + amount);
                        Logger.Log("Payment Type  : " + payment_type.ToUpper());
                        Logger.Log("Date          : " + txn_date_time);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");

                        result = POSManager.Instance.GetLastApprovedTransaction(merchant, terminal, orderNo, amount, "LAST APPROVED TRANSACTION");
                        
                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");


                        return result;

                        break;

                    case "cancel":


                        if (dictData["amount"] != null)
                            amount = Decimal.Parse(dictData["amount"].ToString());
                        else
                            throw new Exception(String.Format("Missing parameter 'amount'!!!"));

                        merchant = string.Empty;
                        if (dictData["merchant"] != null)
                            merchant = dictData["merchant"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'merchant'!!!"));

                        terminal = string.Empty;
                        if (dictData["terminal"] != null)
                            terminal = dictData["terminal"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'terminal'!!!"));


                        if (dictData["orderNo"] != null)
                            orderNo = dictData["orderNo"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'orderNo'!!!"));


                        if (dictData["payment_type"] != null)
                            payment_type = dictData["payment_type"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'payment_type'!!!"));


                        if (dictData["txn_date_time"] != null)
                            txn_date_time = dictData["txn_date_time"].ToString();
                        else
                            throw new Exception(String.Format("Missing parameter 'txn_date_time'!!!"));

                        Logger.Log("Command       : " + functionName);
                        Logger.Log("Merchant Key  : " + merchant);
                        Logger.Log("Terminal      : " + terminal);
                        Logger.Log("OrderNo       : " + orderNo);
                        Logger.Log("Amount        : " + amount);
                        Logger.Log("Payment Type  : " + payment_type);
                        Logger.Log("Date          : " + txn_date_time);
                        Logger.Log("Request       : " + parsedData);
                        Logger.Log("");
                        Logger.Log("***************************************************************");

                        result = POSManager.Instance.CancelTransaction(merchant, terminal, orderNo, amount, payment_type);

                        Logger.Log("***************************************************************");
                        Logger.Log("Response      : \n" + Logger.SerializeObject(result));
                        Logger.Log(" <<<<<<<<<<<<<<<<<<< END OF TRANSACTION <<<<<<<<<<<<<<<<<<<");

                        return result;


                        break;
                    default:
                        result = POSManager.Instance.CancelTransaction(merchant, terminal, "0", amount, "none");

                        Logger.Log("Response      : " + Logger.SerializeObject(result));

                        return result;
                        break;
                }
            }
            catch (Exception ex)
            {
                //Logger.Log("ERROR Message {0}" + ex.Message);
                //Logger.Log("ERROR StackTrace {0}" + ex.StackTrace.ToString());

                response.result = false;
                response.message = ex.Message;

                //response = ErrorProcessor(ex, parsedData);
                Logger.Log("Socket was Closed.Incorrect Format Request.");
                return new Result() { result = false, message = "Incorrect Format Request.", data = transactionResponse };
            }

            return response;
        }

        public static int checkSession(String session)
        {
            int count = 0;
            if (connect.OpenConnection())
            {
                count = connect.CountPaymentBySession(session);
            }

            return count;
        }
        private static Result ErrorProcessor(Exception ex, string parsedData)
        {
            var response = new Result
            {
                result = false,
                message = ex.Message
            };

            try
            {

                /*
                //DO RE-LOGIN IF NO ONE LOGGED IN
                if (ex.Message.Contains(AlohaError.NoOneLoggedIn))
                {
                    Login();
                    response = FuncNavigator(parsedData);
                    return response;
                }
                //IF ALREADY CLOCKED IN, RE-CALL FUNCTIONS TO ALOHA
                if (ex.Message.Contains(AlohaError.AlreadyClockedIn))
                {
                    response = FuncNavigator(parsedData);
                    return response;
                }
                //IF STAFF IS NOT CLOCKED IN, CALL CLOCK IN
                if (ex.Message.Contains(AlohaError.NotClockedIn))
                {
                    Logger.InfoFormat("Error - Not Clocked In");
                    ClockIn();
                    response = FuncNavigator(parsedData);
                    return response;
                }*/


                //_caughtException = true;

                return response;
            }
            catch (Exception e)
            {
                response.result = false;
                response.message = e.Message;
                return response;
            }

        }
    }
    
}

