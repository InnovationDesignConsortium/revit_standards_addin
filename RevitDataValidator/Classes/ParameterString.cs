﻿using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    [DebuggerDisplay("{Name} {NewValue}")]
    public class ParameterString
    {
        public ParameterString(Parameter parameter, string newValue, string oldValue = null)
        {
            Parameter = parameter;
            NewValue = newValue;
            OldValue = oldValue;
        }

        public string Name => Parameter.Definition.Name;

        public Parameter Parameter { get; set; }
        public string NewValue { get; set; }
        public string OldValue { get; set; }
    }
}