using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;
using System.Configuration;
using System.Net.NetworkInformation;
using System.Globalization;

namespace PaymentManager
{

    class Program 
    {
        static NotifyIcon notifyIcon = new NotifyIcon();
        static bool Visible = true;
        private static bool isclosing = false;
        

        public static String IP_TERMINAL = ConfigurationSettings.AppSettings["IP_TERMINAL"];
        public static String PORT_TERMINAL = ConfigurationSettings.AppSettings["PORT_TERMINAL"];
        public static String RUN_MODE = ConfigurationSettings.AppSettings["RUN_MODE"].ToUpper();
        public static string ServiceName = ConfigurationSettings.AppSettings["SERVICE_NAME"].ToUpper();
        public static string PAYMENT_VENDOR = ConfigurationSettings.AppSettings["PAYMENT_VENDOR"].ToUpper();

        public static log4net.ILog _logger = log4net.LogManager.GetLogger(typeof(Program));

        public static DateTime localDate = DateTime.Now;
        public static CultureInfo culture = new CultureInfo("en-US");
        //public static string now = localDate.ToString(culture);
        public static string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");


        [DllImport("user32.dll")]
        internal static extern bool SendMessage(IntPtr hWnd, Int32 msg, Int32 wParam, Int32 lParam);
        static Int32 WM_SYSCOMMAND = 0x0112;
        static Int32 SC_MINIMIZE = 0x0F020;

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        //hidden close button
        private const int MF_BYCOMMAND = 0x00000000;
        public const int SC_CLOSE = 0xF060;

        [DllImport("user32.dll")]
        public static extern int DeleteMenu(IntPtr hMenu, int nPosition, int wFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();
        //end hidden

        //hide or show console
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AllocConsole();

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;
        //end hide/show

        


        [STAThread]
        static void Main(string[] args)
        {
            if (RUN_MODE == "DEBUG")
            {
                //diasbled close button
                //DeleteMenu(GetSystemMenu(GetConsoleWindow(), false), SC_CLOSE, MF_BYCOMMAND);

                _logger.Info("Start Services...");

                if (PAYMENT_VENDOR != "MAYBANK")
                {
                    if (checkConnection(IP_TERMINAL))
                    {

                        SendMessage(Process.GetCurrentProcess().MainWindowHandle, WM_SYSCOMMAND, SC_MINIMIZE, 0);

                        HideConsoleWindow();
                        
                        //log4net.Config.XmlConfigurator.Configure();

                        new MainFlow();

                    }
                }
                else
                {

                    //set to minimize
                    SendMessage(Process.GetCurrentProcess().MainWindowHandle, WM_SYSCOMMAND, SC_MINIMIZE, 0);


                    //hidden in taskbar
                    HideConsoleWindow();

                    //set load log
                    //log4net.Config.XmlConfigurator.Configure();

                    new MainFlow();

                }

                SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);

                setToTray();

                Console.WriteLine("CTRL+C,CTRL+BREAK or suppress the application to exit");

                while (!isclosing) ;


                Console.ReadLine();


            }
            else if(RUN_MODE=="SERVICES")
            {
                /*
                log4net.Config.XmlConfigurator.Configure();
                new MainFlow();
                Application.Run();
                */
                // running as service
                using (var service = new Service())
                {
                    ServiceBase.Run(service);
                }
            }


        }

        public static bool checkConnection(String IP)
        {
            int x = 1;
            bool resp = false;

            while (true)
            {
                Console.WriteLine(now + " Trying connect to terminal ... ");

                Ping ping = new Ping();
                PingReply pingresult = ping.Send(IP, 500);

                Thread.Sleep(5000);

                if (pingresult.Status.ToString() == "Success")
                {
                    Console.WriteLine(now + " " + IP_TERMINAL + ":" + PORT_TERMINAL + " Connected");
                    resp = true;
                    break;
                }
                else
                {
                    Console.WriteLine(now + " " + IP_TERMINAL + ":" + PORT_TERMINAL + " Disconnected");
                    resp = false;
                }
                /*
                if (x == 10)
                {
                    resp = false;
                    Console.WriteLine(now + " Please Connect terminal to the correct network!");
                    break;
                }
                */
                x++;
            }

            return resp;

        }
        public static void ShowConsoleWindow()
        {
            var handle = GetConsoleWindow();

            if (handle == IntPtr.Zero)
            {
                AllocConsole();
            }
            else
            {
                ShowWindow(handle, SW_SHOW);
            }
        }

        public static void HideConsoleWindow()
        {
            var handle = GetConsoleWindow();
            ShowWindow(handle, SW_HIDE);
        }

        public static void SetConsoleWindowVisibility(bool visible)
        {
            IntPtr hWnd = FindWindow(null, Console.Title);
            if (hWnd != IntPtr.Zero)
            {
                if (visible) ShowWindow(hWnd, 1); //1 = SW_SHOWNORMAL           
                else ShowWindow(hWnd, 0); //0 = SW_HIDE               
            }
        }

        public static string ReadPassword()
        {
            string password = "";
            ConsoleKeyInfo info = Console.ReadKey(true);
            while (info.Key != ConsoleKey.Enter)
            {
                if (info.Key != ConsoleKey.Backspace)
                {
                    Console.Write("*");
                    password += info.KeyChar;
                }
                else if (info.Key == ConsoleKey.Backspace)
                {
                    if (!string.IsNullOrEmpty(password))
                    {
                        // remove one character from the list of password characters
                        password = password.Substring(0, password.Length - 1);
                        // get the location of the cursor
                        int pos = Console.CursorLeft;
                        // move the cursor to the left by one character
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                        // replace it with space
                        Console.Write(" ");
                        // move the cursor to the left by one character again
                        Console.SetCursorPosition(pos - 1, Console.CursorTop);
                    }
                }
                info = Console.ReadKey(true);
            }

            // add a new line because user pressed enter at the end of their password
            Console.WriteLine();
            return password;
        }

        private static void setToTray()
        {
            var file = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) +
                       "\\icon\\logo.ico";

            notifyIcon.Click += (s, e) =>
            {
                Visible = !Visible;
                SetConsoleWindowVisibility(Visible);
            };
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            notifyIcon.Visible = true;
            notifyIcon.Icon = new Icon(file);
            notifyIcon.Text = Application.ProductName;

            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add("Exit", null, (s, e) => { Application.Exit(); });
            notifyIcon.ContextMenuStrip = contextMenu;

            //Console.WriteLine("Running!");

            // Standard message loop to catch click-events on notify icon
            // Code after this method will be running only after Application.Exit()
            Application.Run();

            notifyIcon.Visible = false;
            //end hide to tray


        }

        private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)

        {

            // Put your own handler here

            switch (ctrlType)

            {

                case CtrlTypes.CTRL_C_EVENT:

                    isclosing = true;

                    Console.WriteLine("CTRL+C received!");

                    break;
                    

                case CtrlTypes.CTRL_BREAK_EVENT:

                    isclosing = true;

                    Console.WriteLine("CTRL+BREAK received!");

                    break;



                case CtrlTypes.CTRL_CLOSE_EVENT:

                    isclosing = true;
                    var result = MessageBox.Show(
                                            "Close this service?", "Confirm",
                                            MessageBoxButtons.OKCancel, MessageBoxIcon.Information);

                    if (result.Equals(DialogResult.OK))
                    {
                        //please input credential before close
                        Console.WriteLine("yes");
                        Thread.Sleep(10000);
                        
                    }
                    else
                    {
                        Console.WriteLine("no");
                        Thread.Sleep(10000);
                    }
                    break;
                    

                case CtrlTypes.CTRL_LOGOFF_EVENT:

                case CtrlTypes.CTRL_SHUTDOWN_EVENT:

                    isclosing = true;
                    
                    break;



            }

            return true;

        }

        public static void Start(string[] args)
        {
            File.AppendAllText(@"c:\temp\MyService.txt", String.Format("{0} started{1}", DateTime.Now, Environment.NewLine));
        }

        public static void Stop()
        {
            File.AppendAllText(@"c:\temp\MyService.txt", String.Format("{0} stopped{1}", DateTime.Now, Environment.NewLine));
        }

        #region unmanaged

        // Declare the SetConsoleCtrlHandler function

        // as external and receiving a delegate.



        [DllImport("Kernel32")]

        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);



        // A delegate type to be used as the handler routine

        // for SetConsoleCtrlHandler.

        public delegate bool HandlerRoutine(CtrlTypes CtrlType);



        // An enumerated type for the control messages

        // sent to the handler routine.

        public enum CtrlTypes

        {

            CTRL_C_EVENT = 0,

            CTRL_BREAK_EVENT,

            CTRL_CLOSE_EVENT,

            CTRL_LOGOFF_EVENT = 5,

            CTRL_SHUTDOWN_EVENT

        }



        #endregion

    }
}
