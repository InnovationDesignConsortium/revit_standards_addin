using System;

namespace VCExtensibleStorageExtension.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FieldAttribute : Attribute
    {
        public FieldAttribute()
        {
            SpecTypeId = "";
        }

        public string Documentation { get; set; }
        public string SpecTypeId { get; set; }
    }
}