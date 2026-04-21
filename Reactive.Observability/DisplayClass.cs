using System;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Reactive.Observability;

internal static class DisplayClass
{
    public static bool IsDisplayClass(Type type)
    {
        return Attribute.IsDefined(type, typeof(CompilerGeneratedAttribute))
            && type.Attributes.HasFlag(TypeAttributes.NotPublic)
            && type.Name is ['<', '>', 'c', ..] or ['V', 'B', '$', ..]
            && type.Name.Contains("DisplayClass", StringComparison.Ordinal);
    }
}
