using log4net;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;


namespace PaymentManager
{
    static class Logger
    {
        public static readonly ILog LogInstance = LogManager.GetLogger(typeof(Program));

        public static void Log(string msg)
        {
            try
            {
                //Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo().

                //Console.OutputEncoding = Encoding.UTF8;
                CultureInfo culture = CultureInfo.CreateSpecificCulture("en-US");
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;

                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                Logger.LogInstance.Info(msg);
                //Logger.LogInstance.Error(msg);
                Console.WriteLine(msg);
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
        }

        public static void RecatchException(object responseObj)
        {

            string placeOrderInString = Newtonsoft.Json.JsonConvert.SerializeObject(responseObj);
            throw new Exception(placeOrderInString);

        }

        public static string SerializeObject(object responseObj)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(responseObj);
        }

        public static void Log(Exception e)
        {
            try
            {
                Logger.Log(string.Empty);
                Logger.LogInstance.Info(">>>>>>>> Exception:" + e.Message + e.StackTrace.ToString());

                //MessageBox.Show(e.Message);

                Console.WriteLine(">>>>>>>> Exception:" + e.Message + e.StackTrace.ToString());

                //Logger.LogInstance.Error(">>>>>>>> Exception:" + e.Message + e.StackTrace.ToString());
                //Console.WriteLine(">>>>>>>> Exception:" + e.Message + e.StackTrace.ToString());
            }
            catch (System.Exception ex)
            {
                throw ex;
            }
        }

        public static void WriteToFile(string msg)
        {
            try
            {
                using (StreamWriter outputFile = new StreamWriter(@"Output_Log_.txt"))
                {
                    outputFile.WriteLine(msg);
                }
            }
            catch (Exception ex)
            {
                Logger.Log(ex);
            }
        }
    }

    public class ErrorWrapper
    {

        public int LoggingTargetType { get; set; }

        public int LoggingTargetId { get; set; }

        public string Content { get; set; }

        public Exception ExceptionObject { get; set; }

        public string RawChangeContent { get; set; }

        public int ActionType { get; set; }

        public ErrorWrapper()
        {
        }

        public static void HandleExceptionViaEmail(Exception ex)
        {
            try
            {
                //Utils.SmtpLogger.Error(String.Format("[TRANSACTION DATA] [SERVICE END POINT] : \r\n {1} \r\n {2}", ex.ToString(), ex.StackTrace.ToString()));
            }
            catch (Exception e)
            {
            }
        }

    }
}
