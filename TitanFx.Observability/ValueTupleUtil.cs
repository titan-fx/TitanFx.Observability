using System;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TitanFx.Observability;

internal static class ValueTupleUtil
{
    private static readonly Type[] _tValueTuple =
    [
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    ];

    public static (Type Type, ITuple Value) Create(
        ReadOnlySpan<(Type ItemType, object? Value)> items
    )
    {
        var (type, value) = CombineTailRecursive(
            items,
            static () => (typeof(ValueTuple), new ValueTuple()),
            _tValueTuple,
            static (t, items) =>
            {
                t = t.MakeGenericType(items.Map(static x => x.ItemType));
                return (t, Activator.CreateInstance(t, items.Map(static x => x.Value)));
            }
        )!;
        return (type, Unsafe.As<ITuple>(value!));
    }

    private static T CombineTailRecursive<D, T>(
        ReadOnlySpan<T> source,
        Func<T> baseCase,
        ReadOnlySpan<D> data,
        Func<D, ReadOnlySpan<T>, T> combine
    )
    {
        if (source.Length == 0)
            return baseCase();
        if (source.Length >= data.Length)
        {
            return combine(
                data[^1],
                [
                    .. source[..(data.Length - 1)],
                    CombineTailRecursive(source[(data.Length - 1)..], baseCase, data, combine),
                ]
            );
        }
        return combine(data[source.Length - 1], source);
    }

    public static Type[] Elements(Type type)
    {
        if (!type.IsGenericType)
        {
            if (type == typeof(ValueTuple))
                return [];
        }
        else
        {
            var args = type.GetGenericArguments();
            var open = type.GetGenericTypeDefinition();
            return args.Length switch
            {
                1 when open == typeof(ValueTuple<>) => args,
                2 when open == typeof(ValueTuple<,>) => args,
                3 when open == typeof(ValueTuple<,,>) => args,
                4 when open == typeof(ValueTuple<,,,>) => args,
                5 when open == typeof(ValueTuple<,,,,>) => args,
                6 when open == typeof(ValueTuple<,,,,,>) => args,
                7 when open == typeof(ValueTuple<,,,,,,>) => args,
                8 when open == typeof(ValueTuple<,,,,,,,>) => [.. args[..7], .. Elements(args[7])],
                _ => throw new ArgumentException("Type is not a valid ValueTuple", nameof(type)),
            };
        }

        throw new ArgumentException("Type is not a valid ValueTuple", nameof(type));
    }

    public static Expression[] Fields(Expression valueTuple)
    {
        var type = valueTuple.Type;
        if (!type.IsGenericType)
        {
            if (type == typeof(ValueTuple))
                return [];
        }
        else
        {
            var fields = type.GetFields();
            var expr = fields.Map(valueTuple, Expression.Field);
            var open = type.GetGenericTypeDefinition();
            return fields.Length switch
            {
                1 when open == typeof(ValueTuple<>) => expr,
                2 when open == typeof(ValueTuple<,>) => expr,
                3 when open == typeof(ValueTuple<,,>) => expr,
                4 when open == typeof(ValueTuple<,,,>) => expr,
                5 when open == typeof(ValueTuple<,,,,>) => expr,
                6 when open == typeof(ValueTuple<,,,,,>) => expr,
                7 when open == typeof(ValueTuple<,,,,,,>) => expr,
                8 when open == typeof(ValueTuple<,,,,,,,>) => [.. expr[..7], .. Fields(expr[7])],
                _ => throw new ArgumentException(
                    "Type is not a valid ValueTuple",
                    nameof(valueTuple)
                ),
            };
        }

        throw new ArgumentException("Type is not a valid ValueTuple", nameof(valueTuple));
    }
}
