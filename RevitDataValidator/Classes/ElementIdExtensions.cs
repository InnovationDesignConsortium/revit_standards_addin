using Autodesk.Revit.DB;

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
#if R2023 || R2022
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
#if R2023 || R2022
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