using System;
using System.Collections.Generic;

namespace TitanFx.Observability;

internal static class SpanMappingExtensions
{
    public static TOut[] Map<TIn, TArg, TOut>(
        this ReadOnlySpan<TIn> source,
        TArg arg,
        Func<TArg, TIn, TOut> mapper
    )
    {
        var result = new TOut[source.Length];
        for (var i = 0; i < source.Length; i++)
            result[i] = mapper(arg, source[i]);
        return result;
    }

    public static TOut[] Map<TIn, TOut>(this ReadOnlySpan<TIn> source, Func<TIn, TOut> mapper)
    {
        var result = new TOut[source.Length];
        for (var i = 0; i < source.Length; i++)
            result[i] = mapper(source[i]);
        return result;
    }

    public static TOut[] Map<TIn, TOut>(
        this IReadOnlyCollection<TIn> source,
        Func<TIn, TOut> mapper
    )
    {
        var result = new TOut[source.Count];
        var i = 0;
        foreach (var item in source)
            result[i++] = mapper(item);
        return result;
    }
}
