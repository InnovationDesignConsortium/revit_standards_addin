﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public abstract class BaseRule : IRule
    {
        public Guid Guid { get; set; }

        [JsonProperty("Categories")]
        public List<string> Categories { get; set; }

        [JsonProperty("When Run")]
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