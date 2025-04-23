using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public abstract class BaseRule : IRule
    {
        public Guid Guid { get; set; }

        [JsonProperty("Rule Name")]
        public string RuleName { get; set; }

        public bool Disabled {  get; set; }

        [JsonProperty("Disable By Default")]
        public bool DisableByDefault { get; set; }

        [JsonProperty("Categories")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> Categories { get; set; }

        [JsonProperty("When Run")]
        [JsonConverter(typeof(StringOrArrayConverter))]
        public List<string> WhenRun { get; set; }

        public IEnumerable<WhenToRun> WhenToRun
        {
            get
            {
                var ret = new List<WhenToRun>();
                if (WhenRun == null)
                {
                    return new List<WhenToRun> { RevitDataValidator.WhenToRun.Realtime };
                }

                foreach (string s in WhenRun)
                {
                    if (Enum.TryParse(s, out WhenToRun whenToRun))
                    {
                        ret.Add(whenToRun);
                    }
                }
                return ret;
            }
        }
    }
}