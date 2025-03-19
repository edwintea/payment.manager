using System;
using Newtonsoft.Json;

namespace PaymentManager.DataContracts
{
    public class Result
    {
        public Object data;
        public string message = "";
        public string nextCommand = "";
        public bool result = true;

        public string ToJsonString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}