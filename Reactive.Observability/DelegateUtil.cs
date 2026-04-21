using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Reactive.Observability;

public static class DelegateUtil
{
    [return: NotNullIfNotNull(nameof(a))]
    [return: NotNullIfNotNull(nameof(b))]
    public static T? Combine<T>(T? a, T? b)
        where T : Delegate
    {
        return Unsafe.As<T>(Delegate.Combine(a, b));
    }
}
