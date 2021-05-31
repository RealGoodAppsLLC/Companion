using System;

namespace RealGoodApps.Companion.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, AllowMultiple = true)]
    public class CompanionTypeAttribute : Attribute
    {
        public Type Type { get; }

        public string FullyQualifiedName { get; }

        public CompanionTypeAttribute(Type type)
        {
            Type = type;
        }

        public CompanionTypeAttribute(string fullyQualifiedName)
        {
            FullyQualifiedName = fullyQualifiedName;
        }
    }
}
