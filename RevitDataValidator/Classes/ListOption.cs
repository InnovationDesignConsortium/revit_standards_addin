using Newtonsoft.Json;

namespace RevitDataValidator
{
    public partial class ListOption
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("Filter Value")]
        public string FilterValue { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}