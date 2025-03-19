using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tabsquare.Payment;

namespace PaymentManager
{
    class POSManager
    {
        private static PaymentInterface _instance = null;

        public static PaymentInterface Instance
        {
            get
            {
                if (_instance == null)
                    LoadPOSPlugin();

                return _instance;
            }
        }

        public static void LoadPOSPlugin()
        {

            string payment_vendor = string.Empty;
            try
            {
                payment_vendor = System.Configuration.ConfigurationManager.AppSettings["PAYMENT_VENDOR"];
            }
            catch (Exception ex)
            {
                Logger.Log(ex.Message);
            }

            if (string.IsNullOrEmpty(payment_vendor))
                throw new Exception("PARAM POS_TYPE is EMPTY!!!");

            payment_vendor = payment_vendor.ToUpper();

            switch (payment_vendor)
            {
                
                case "TYRO":
                    _instance = new Tabsquare.Payment.TyroImplementation(); ;
                    break;
                case "BCA":
                    _instance = new Tabsquare.Payment.BCAImplementation(); ;
                    break;
                case "NETTS":
                    _instance = new Tabsquare.Payment.NETTSImplementation(); ;
                    break;
                case "OCBC":
                    _instance = new Tabsquare.Payment.OCBCImplementation(); ;
                    break;
                case "UOB":
                    _instance = new Tabsquare.Payment.UOBImplementation(); ;
                    break;
                case "MAYBANK":
                    _instance = new Tabsquare.Payment.MAYBANKImplementation(); ;
                    break;
            }

        }

    }
}
