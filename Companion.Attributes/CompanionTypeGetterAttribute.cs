using System;

namespace RealGoodApps.Companion.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class CompanionTypeGetterAttribute : Attribute
    {
        public Type Type { get; }

        public string FullyQualifiedName { get; }

        public CompanionTypeGetterAttribute(Type type)
        {
            Type = type;
        }

        public CompanionTypeGetterAttribute(string fullyQualifiedName)
        {
            FullyQualifiedName = fullyQualifiedName;
        }
    }
}
