using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public class CustomFailureWrapper
    {
        public List<CustomFailure> CustomFailures { get; set; }
    }
    public class CustomFailure
    {
        public Guid FailureGuid { get; set; }
        public string Message { get; set; }
    }
}