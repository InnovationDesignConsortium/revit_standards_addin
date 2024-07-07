using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitDataValidator
{
    public abstract class StateParameter<T> : IStateParameter
    {
        public List<Parameter> Parameters { get; set; }
        public object Value { get; set; }
        public string State { get; set; }
        public string Name { get; set; }
        public bool IsEnabled { get; set; }
    }

    public class BoolStateParameter : StateParameter<bool>
    { }

    public class TextStateParameter : StateParameter<string>
    { }

    public class ChoiceStateParameter : StateParameter<object>
    {
        public List<StringInt> Choices { get; set; }
        public StringInt SelectedChoice { get; set; }
    }

    public class PushButtonParameter : StateParameter<object>
    {
    }
}