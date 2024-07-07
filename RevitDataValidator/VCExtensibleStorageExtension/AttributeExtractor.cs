using System;
using System.Reflection;

namespace VCExtensibleStorageExtension
{
    /// <summary>
    /// Helper class which helps to find value if the TAttribute of a member info
    /// </summary>
    /// <typeparam name="TAttribute"></typeparam>
    [System.Reflection.Obfuscation(Exclude = true, ApplyToMembers = true)]
    internal class AttributeExtractor<TAttribute> where TAttribute : Attribute
    {
        public TAttribute GetAttribute(MemberInfo memberInfo)
        {
            var attributes = memberInfo.GetCustomAttributes(typeof(TAttribute), false);

            if (attributes.Length == 0)
                throw new InvalidOperationException(string.Format("MemberInfo {0} does not have a {1}", memberInfo, typeof(TAttribute)));

            var atribute = attributes[0] as TAttribute;
            if (atribute == null)
                throw new InvalidOperationException(string.Format("MemberInfo {0} does not have a {1}", memberInfo, typeof(TAttribute)));

            return atribute;
        }
    }
}