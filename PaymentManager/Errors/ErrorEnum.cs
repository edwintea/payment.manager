using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tabsquare.Payment
{
    class ErrorEnum
    {
        public static string TABSQUARE_CONNECTIVITY_ERROR = "";

        public static string TABSQUARE_ADDORDER_DESTINATIONFOLDER_NOT_FOUND = "The destination folder {0} is unreachable!";
        public static string TABSQUARE_ADDORDER_PLU_NOT_FOUND = "The item {0} does not have the required code!";

        public static string TABSQUARE_CONFIG_NOT_FOUND = "The required configuration param {0} is not found in App.Config";
    }
}
