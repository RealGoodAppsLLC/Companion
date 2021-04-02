using System;

namespace RealGoodApps.Companion.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Method, AllowMultiple = true)]
    public class CompanionTypeAttribute : Attribute
    {
        public Type Type { get; }

        public CompanionTypeAttribute(Type type)
        {
            Type = type;
        }
    }
}
