using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public interface IRule
    {
        List<string> Categories { get; set; }
        Guid Guid { get; set; }
    }
}