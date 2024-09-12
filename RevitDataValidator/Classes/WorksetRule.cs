using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class WorksetRule : BaseRule
    {
        public string Workset { get; set; }
        public List<ParameterData> Parameters { get; set; }

        public override string ToString()
        {
            return $"'{Workset}' [{string.Join(",", Categories)}] [{string.Join(",", Parameters)}]";
        }
    }
}