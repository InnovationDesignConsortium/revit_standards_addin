using Newtonsoft.Json;

namespace RevitDataValidator
{
    public partial class ListOption
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}