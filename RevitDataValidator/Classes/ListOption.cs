using CsvHelper.Configuration.Attributes;
using Newtonsoft.Json;

namespace RevitDataValidator
{
    public partial class ListOption
    {
        [Index(0)]
        [Default(null)]
        [JsonProperty("name")]
        public string Name { get; set; }

        [Index(2)]
        [JsonProperty("description")]
        [Default(null)]
        public string Description { get; set; }

        [Index(1)]
        [JsonProperty("Filter Value")]
        [Default(null)]
        public string FilterValue { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}