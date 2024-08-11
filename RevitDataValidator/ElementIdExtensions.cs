using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    internal static class ElementIdExtension
    {
        public static bool IsValid(this ElementId elementId)
        {
            if (elementId is null)
                return false;

            return elementId != ElementId.InvalidElementId;
        }

        public static bool IsInvalid(this ElementId elementId)
        {
            return !elementId.IsValid();
        }

        public static long GetValue(this ElementId elementId)
        {
#if REVIT2023 || REVIT2022 || REVIT2021 || REVIT2020 || REVIT2019 || REVIT2018 || REVIT2017
            return elementId.IntegerValue;
#else
            return elementId.Value;
#endif
        }

        public static BuiltInCategory GetBuiltInCategory(this ElementId elementId)
        {
            return (BuiltInCategory)elementId.GetValue();
        }

        public static BuiltInParameter GetBuiltInParameter(this ElementId elementId)
        {
            return (BuiltInParameter)elementId.GetValue();
        }
    }

    internal static class BuiltInCategoryExtension
    {
        public static ElementId GetElementId(this BuiltInCategory categoryId)
        {
            return ElementIdUtils.New(categoryId);
        }
    }

    internal static class ElementIdUtils
    {
        public static ElementId New(long value)
        {
#if REVIT2023 || REVIT2022 || REVIT2021 || REVIT2020 || REVIT2019 || REVIT2018 || REVIT2017
            return new ElementId((int)value);
#else
            return new ElementId(value);
#endif
        }

        public static ElementId New(BuiltInParameter parameterId)
        {
            return new ElementId(parameterId);
        }

        public static ElementId New(BuiltInCategory categoryId)
        {
            return new ElementId(categoryId);
        }
    }
}
