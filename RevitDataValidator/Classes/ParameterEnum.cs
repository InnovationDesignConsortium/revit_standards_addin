using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitDataValidator.Classes
{
    public partial class ParameterEnum
    {
        [JsonProperty("typeid")]
        public string Typeid { get; set; }

        [JsonProperty("inherits")]
        public List<string> Inherits { get; set; }

        [JsonProperty("properties")]
        public List<Property> Properties { get; set; }
    }

    public partial class Property
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("value")]
        public int Value { get; set; }
    }
}
