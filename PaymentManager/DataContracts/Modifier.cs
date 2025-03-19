
using Newtonsoft.Json;
using System.Collections.Generic;

namespace PaymentManager.DataContracts
{
    public class Modifier
    {
        public int id;
        public string name = "";
        public double price;
        public int quantity = 1;

        public List<Modifier> modifiers;

        public Modifier()
        {
            modifiers = new List<Modifier>();
        }

        public Modifier(int _id, string _name, double _price, int _quantity)
        {
            this.id = _id;
            this.name = _name;
            this.price = _price;
            this.quantity = _quantity;

            modifiers = new List<Modifier>();
        }

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}