using System;

namespace Inedo.Romp.Configuration
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    internal sealed class NotCascadedAttribute : Attribute
    {
    }
}