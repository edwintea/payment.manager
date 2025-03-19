using System.Collections.Generic;
using Newtonsoft.Json;

namespace PaymentManager.DataContracts
{
    public class OrderItem
    {
        public List<Modifier> modifiers;
        public string name = "";
        public int plu;
        public double price;
        public double amount;
        public double quantity = 1;
        public int ui_type = 1;

        public double order_net_amount = 0;
        public double order_gst = 0;
        public double order_grand_amount = 0;

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}