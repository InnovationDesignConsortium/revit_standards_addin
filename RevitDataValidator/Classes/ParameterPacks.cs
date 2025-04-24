using Newtonsoft.Json;
using System.Collections.Generic;
using System.Diagnostics;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {Category}")]
    public class PackSet
    {
        public string Name { get; set; }

        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> Category { get; set; }

        [JsonProperty("Categories")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        private List<string> Categories { set { Category = value; } }

        [JsonProperty("Parameter Packs")]
        public List<string> ParameterPacks { get; set; }

        [JsonProperty("Show All Other Parameters")]
        public bool ShowAllOtherParameters { get; set; }

        [JsonProperty("Show All Other Parameters Excluding")]
        public List<string> ShowAllOtherParametersExcluding { get; set; }
    }

    [DebuggerDisplay("{Name} {Category}")]
    public class ParameterPack
    {
        public string Name { get; set; }

        [JsonProperty("Categories")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        private List<string> Categories { set { Category = value; } }

        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> Category { get; set; }
        public List<string> Parameters { get; set; }
        public string URL { get; set; }
        public string PDF { get; set; }
        public string Video { get; set; }

        [JsonProperty("Custom Tools")]
        public List<string> CustomTools { get; set; }
    }

    public class ParameterUIData
    {
        [JsonProperty("Parameter Packs")]
        public List<ParameterPack> ParameterPacks { get; set; }

        [JsonProperty("Pack Sets")]
        public List<PackSet> PackSets { get; set; }
    }
}