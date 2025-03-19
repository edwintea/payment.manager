using System.Collections.Generic;
using Newtonsoft.Json;
using System;

namespace PaymentManager.DataContracts
{
    public class TransactionResponse
    {
        public long STAN { get; set; }

        public string MerchantId { get; set; }

        public string TerminalID { get; set; }

        public DateTime BankDateTime { get; set; }

        public string TxnRef { get; set; }

        public string CardPAN { get; set; }

        public string Receipt { get; set; }

        public string CardType { get; set; }

        public string AuthCode { get; set; }

        public string status { get; set; }

        public string ID { get; set; }

        public string result { get; set; }

        public double surcharge { get; set; }

        public string ecrTrxNo { get; set; }

        public string invoiceNo { get; set; }

        public string batchNo { get; set; }

        public string expireDate { get; set; }

        public string APP { get; set; }

        public string AID { get; set; }

        public string TC { get; set; }

        public string TVR { get; set; }
        
        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}