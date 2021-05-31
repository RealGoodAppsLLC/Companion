using System;

namespace RealGoodApps.Companion.Attributes
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class CompanionTypeSetterAttribute : Attribute
    {
        public Type Type { get; }

        public string FullyQualifiedName { get; }

        public CompanionTypeSetterAttribute(Type type)
        {
            Type = type;
        }

        public CompanionTypeSetterAttribute(string fullyQualifiedName)
        {
            FullyQualifiedName = fullyQualifiedName;
        }
    }
}
