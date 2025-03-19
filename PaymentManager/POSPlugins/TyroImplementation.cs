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
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using Tyro.Integ;
using Tyro.Integ.Domain;
using Tyro.Common.Network;
using System.Net.Sockets;

namespace Tabsquare.Payment
{
    
    public class TyroImplementation : PaymentInterface
    {

        public enum LogType { Info, Error, Warning };
        public enum TransactionStatus { Paid, Failed, Cancelled };
        private TransactionStatus isSuccessfully = TransactionStatus.Failed;

        protected JavaScriptSerializer _javaScriptSerializer;

        private TransactionResponse transactionResponse;
        private TransactionResult transactionResult { get; set; }
        private bool isError = false;
        private bool isCancelled = false;
        private String errorMessage = null;
        private String errorCode = null;
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

        public string BPS_IP
        {
            get
            {
                try
                {
                    return ConfigurationManager.AppSettings["BPS_IP"].ToString();
                }
                catch (Exception)
                {
                    throw new Exception(String.Format(ErrorEnum.TABSQUARE_CONFIG_NOT_FOUND, "BPS_IP"));
                }
            }
        }

        public int BPS_PORT
        {
            get
            {
                try
                {
                    return Int32.Parse(ConfigurationManager.AppSettings["BPS_PORT"].ToString());
                }
                catch (Exception)
                {
                    throw new Exception(String.Format(ErrorEnum.TABSQUARE_CONFIG_NOT_FOUND, "BPS_PORT"));
                }
            }
        }

        public string MERCHANT_CODE
        {
            get
            {
                try
                {
                    return ConfigurationManager.AppSettings["MERCHANT_CODE"].ToString();
                }
                catch (Exception)
                {
                    throw new Exception(String.Format(ErrorEnum.TABSQUARE_CONFIG_NOT_FOUND, "MERCHANT_CODE"));
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
                return new Result() { result = false, message = "Logon Failed! [" + ex.Message + "]" };
            }

            return new Result() { result = false, message = "Logon Failed!!!" };
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

        public POSInformation posInfo()
        {

            POSInformation posInfo = new POSInformation();
            posInfo.SetProductVendor("Tabsquare kiosk");
            posInfo.SetProductName("Kiosk");
            posInfo.SetProductVersion("2.0");
            posInfo.SetSiteReference("Singapore");

            return posInfo;

        }

        public Result GetTerminalStatus(string merchant, string terminal)
        {
            return new Result() { result = false, message = "Transaction Failed!!!" };
        }

        public Result GetLastApprovedTransaction(string merchant, string terminal, string orderNo, decimal amount, string commandType)
        {
            
            return new Result() { result = false, message = "The transaction failed!!" };
        }

        public Result DoTransaction(string merchant, string terminal, string orderNo, decimal amount, string paymentType)
        {

            bool isSuccessful = false;
            bool isRefund = false;
            isError = false;
            isCancelled = false;
            ResetState();

            try
            {
                POSInformation posInfo = new POSInformation();
                posInfo.SetProductVendor("Tabsquare kiosk");
                posInfo.SetProductName("Kiosk");
                posInfo.SetProductVersion("2.0");
                posInfo.SetSiteReference("Singapore");

                TerminalAdapter adapter = new TerminalAdapter(posInfo);
                adapter.ReceiptReturned += ReceiptReturned;
                adapter.ErrorOccured += ErrorOccured;
                adapter.TransactionCompleted += TransactionCompleted;
                adapter.SetPOSInformation(posInfo);

                TerminalAdapterHeadless c = new TerminalAdapterHeadless();


                String isCent = ConfigurationManager.AppSettings["CENT"].ToString();
                int amountInt = 0;

                if (isCent == "true")
                {
                    amountInt = StandadizeCurrency(amount);
                }
                else
                {
                    amountInt = Convert.ToInt32(amount);
                }

                if (!isError)
                {
                    try
                    {
                        isSuccessful = adapter.Purchase(amountInt, 0);

                        int y = 0;
                        int z = 0;
                        while (isSuccessful) //payment on doing
                        {
                            Thread.Sleep(1000);


                            if (!isError)
                            {


                                if (isCancelled)
                                {
                                    try
                                    {
                                        return new Result()
                                        { result = false, message = errorMessage, data = null };

                                    }
                                    catch (Exception e)
                                    {

                                    }

                                    break;
                                }

                                try
                                {
                                    if (!String.IsNullOrEmpty(transactionResponse.CardType))
                                    {

                                        break;
                                    }

                                }
                                catch (Exception e)
                                {

                                    y++;
                                }

                            }
                            else
                            {
                                return new Result()
                                { result = false, message = errorMessage, data = null };


                                break;
                            }

                            Thread.Sleep(1000);

                            z++;

                            if (z == 30)
                            {

                                //MessageBox.Show("Closed!");

                                return new Result()
                                { result = false, message = "Connection timeout!", data = null };
                            }

                        }

                        if (transactionResponse.AuthCode != "0")
                        {
                            return new Result()
                            { result = isSuccessful, message = isSuccessful ? "Transaction settled successfully!" : "Transaction settled failed!", data = transactionResponse };

                        }
                        else
                        {
                            return new Result()
                            { result = false, message = "Transaction settled failed!", data = transactionResponse };

                        }


                    }
                    catch (Exception r)
                    {
                        Console.WriteLine(r.Message);
                        return new Result()
                        { result = false, message = "Transaction settled failed!", data = null };
                    }

                }
                else
                {
                    return new Result()
                    { result = false, message = "Transaction settled failed!", data = null };

                }


            }
            catch (TyroException ex)
            {

                return new Result()
                { result = isSuccessful, message = ex.Message.ToString(), data = null };

            }

        }

        public Result DoContinueSakuku(decimal amount, long refNo)
        {
            return new Result() { result = true, message = "The transaction settled successfully!", data = transactionResponse };
        }

        public Result DoInquirySakuku(decimal amount, long refNo)
        {
            return new Result() { result = true, message = "The transaction settled successfully!", data = transactionResponse };
        }

        public Result DoSettlement(string merchant, string terminal, string orderNo, decimal amount, string paymentType, string acquirer_bank)
        {

            return new Result() { result = false, message = "Transaction Failed! " };

        }

        public Result CancelTransaction(string merchant, string terminal, string orderNo, decimal amount, string paymentType)
        {

            POSInformation posInfo = new POSInformation();
            posInfo.SetProductVendor("Tabsquare kiosk");
            posInfo.SetProductName("Kiosk");
            posInfo.SetProductVersion("2.0");
            posInfo.SetSiteReference("Singapore");

            TerminalAdapterHeadless adapter = new TerminalAdapterHeadless(posInfo);
            adapter.AttemptCancel();
            adapter.CancelFinished();

            return new Result()
            { result = false, message = "Transaction was Cancelled", data = null };


        }
        private int StandadizeCurrency(decimal amount)
        {
            string amountStr = amount.ToString().Replace(".", "");
            return int.Parse(amountStr);

        }


        private void TransactionCompleted(Transaction transaction)
        {

            Logger.Log(String.Format(
                "Transaction ID: {0} {1} Status: {2} {1} Result: {3}" +
                " {1} Authorisation Code: {4} {1} Transaction Reference: {5} {1} Card Type: {6}",
                transaction.ID, Environment.NewLine, transaction.Status, transaction.Result,
                transaction.AuthorisationCode, transaction.ReferenceNumber, transaction.CardType));

            transactionResponse = new TransactionResponse();

            string authCode = "0";
            var dt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            transactionResponse.ID = transaction.ID;
            transactionResponse.MerchantId = ConfigurationManager.AppSettings["MERCHANT_CODE"].ToString();
            transactionResponse.TerminalID = ConfigurationManager.AppSettings["TERMINAL_ID"].ToString();
            transactionResponse.AuthCode = authCode;
            transactionResponse.BankDateTime = DateTime.Parse(dt);
            transactionResponse.status = transaction.Status;
            transactionResponse.result = transaction.Result;
            transactionResponse.TxnRef = transaction.ReferenceNumber;
            transactionResponse.CardType = ConvertPayment(transaction.CardType);
            transactionResponse.CardPAN = transaction.GetCardToken();
            transactionResponse.Receipt = transaction.GetReceipts()[0];

            if (transaction.Result == "CANCELLED")
            {
                isCancelled = true;
                errorMessage = "Transaction was cancelled.";
            }

            String[] surcharge = transaction.GetReceipts();
            String surc = "0";

            if (surcharge.Length > 0)
            {
                surc = surcharge[0].Trim();
                // EFTPOS HAS NO SURCHARGE
                if (transactionResponse.CardType != "EFTPOS")
                {
                    surc = surc.Split(new string[] { "Surcharge" }, StringSplitOptions.None)[1]
                        .Split(new string[] { "AUD" }, StringSplitOptions.None)[1]
                        .Split(new string[] { "----------" }, StringSplitOptions.None)[0]
                        .Split(new string[] { "$" }, StringSplitOptions.None)[1].Trim();
                }
                else
                {
                    surc = "0";
                }

            }

            transactionResponse.surcharge = Convert.ToDouble(surc);


        }

        public String ConvertPayment(String s)
        {
            String rs = s;

            switch (s)
            {
                case "Visa":
                    rs = "VISA";
                    break;
                case "American Express":
                    rs = "AMEX";
                    break;
                case "MasterCard":
                    rs = "MASTER";
                    break;
                case "Jcb":
                    rs = "JCB";
                    break;
                case "Diners Club":
                    rs = "DINERS";
                    break;
                default:
                    rs = s;
                    break;
            }

            return rs;
        }

        private void ErrorOccured(Error error)
        {

            Logger.Log(String.Format("ERROR: {0} {1} Transaction Started: {2}",
                error.ErrorMessage, Environment.NewLine, error.TransactionStarted));

            isError = true;
            errorCode = error.StatusCode;
            errorMessage = error.ErrorMessage;
        }

        private void ReceiptReturned(Receipt receipt)
        {
            Logger.Log(receipt.Text);

            if (receipt.ToJSON().Contains("CANCELLED"))
            {
                //MessageBox.Show("Canceled");
                isCancelled = true;
                errorMessage = receipt.Text;
            }
            else
            {
                isCancelled = false;
                errorMessage = receipt.Text;
            }

        }

        private void LogPayLoad(byte[] bytes)
        {
            Logger.Log("Number of bytes:" + bytes.Count());

            StringBuilder payLoadInStr = new StringBuilder();
            foreach (byte b in bytes)
            {
                payLoadInStr.Append(b.ToString("x2") + " ");
            }
            Logger.Log(">>>>> :" + payLoadInStr.ToString());
        }

        private void ResetState()
        {

            transactionResponse = null;

        }

        private static void ProcessStringResponse(string dataResponse)
        {
            List<char> chList = dataResponse.ToList();

            Logger.Log("Length of the data:" + chList.Count());

            string byteString = string.Empty;
            foreach (char ch in chList)
            {
                byteString += Convert.ToByte(ch).ToString("x2") + " ";
            }
            Logger.Log("Data Response in Bytes:" + byteString);
            Logger.Log("Data Response in String :" + dataResponse);
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
