using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tabsquare.Payment
{
    class PublicConstant
    {
        public const string ConfigureFile = @"Configure.ini";

        public const string UrlKey = @"url";

        public const string RootSection = @"RootEpoint";
        public const string DirKey = @"Dir";


        // need to think about this if we set timeout to 30 seconds then what about retry time synchronization between client and server????
        public const int SocketTimeOut = 15000000;//100000000; // 1min 40s for test //15000000; // microseconds - 1 seconds

        public static readonly object ClockOrder = new object();
    }
}
