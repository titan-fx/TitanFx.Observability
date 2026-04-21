using System;
using Reactive.Observability.Observables;

namespace Reactive.Observability;

internal static class TypeLookup
{
    public static int MaxArguments => _actions.Length - 1;
    public static int MaxValueTuple => _valueTuple.Length - 1;

    public static Type ValueTuple(ReadOnlySpan<Type> arguments)
    {
        return _valueTuple[arguments.Length].MakeGenericTypeSafe(arguments);
    }

    public static Type Delegate(ReadOnlySpan<Type> arguments, Type returnType)
    {
        return returnType == typeof(void)
            ? _actions[arguments.Length].MakeGenericTypeSafe(arguments)
            : _funcs[arguments.Length].MakeGenericTypeSafe([.. arguments, returnType]);
    }

    public static Type CombineObservable(ReadOnlySpan<Type> arguments, Type returnType)
    {
        return returnType == typeof(void)
            ? _actionObservables[arguments.Length].MakeGenericTypeSafe(arguments)
            : _funcObservables[arguments.Length].MakeGenericTypeSafe([.. arguments, returnType]);
    }

    private static Type MakeGenericTypeSafe(this Type target, ReadOnlySpan<Type> arguments)
    {
        if (!target.IsGenericType && arguments.Length == 0)
            return target;

        return target.MakeGenericType([.. arguments]);
    }

    private static readonly Type[] _valueTuple =
    [
        typeof(ValueTuple),
        typeof(ValueTuple<>),
        typeof(ValueTuple<,>),
        typeof(ValueTuple<,,>),
        typeof(ValueTuple<,,,>),
        typeof(ValueTuple<,,,,>),
        typeof(ValueTuple<,,,,,>),
        typeof(ValueTuple<,,,,,,>),
        typeof(ValueTuple<,,,,,,,>),
    ];

    private static readonly Type[] _funcs =
    [
        typeof(Func<>),
        typeof(Func<,>),
        typeof(Func<,,>),
        typeof(Func<,,,>),
        typeof(Func<,,,,>),
        typeof(Func<,,,,,>),
        typeof(Func<,,,,,,>),
        typeof(Func<,,,,,,,>),
        typeof(Func<,,,,,,,,>),
        typeof(Func<,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,,,,,>),
        typeof(Func<,,,,,,,,,,,,,,,,>),
    ];
    private static readonly Type[] _funcObservables =
    [
        typeof(FuncObservable<>),
        typeof(FuncObservable<,>),
        typeof(FuncObservable<,,>),
        typeof(FuncObservable<,,,>),
        typeof(FuncObservable<,,,,>),
        typeof(FuncObservable<,,,,,>),
        typeof(FuncObservable<,,,,,,>),
        typeof(FuncObservable<,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,,,,,,,>),
        typeof(FuncObservable<,,,,,,,,,,,,,,,,>),
    ];
    private static readonly Type[] _actions =
    [
        typeof(Action),
        typeof(Action<>),
        typeof(Action<,>),
        typeof(Action<,,>),
        typeof(Action<,,,>),
        typeof(Action<,,,,>),
        typeof(Action<,,,,,>),
        typeof(Action<,,,,,,>),
        typeof(Action<,,,,,,,>),
        typeof(Action<,,,,,,,,>),
        typeof(Action<,,,,,,,,,>),
        typeof(Action<,,,,,,,,,,>),
        typeof(Action<,,,,,,,,,,,>),
        typeof(Action<,,,,,,,,,,,,>),
        typeof(Action<,,,,,,,,,,,,,>),
        typeof(Action<,,,,,,,,,,,,,,>),
        typeof(Action<,,,,,,,,,,,,,,,>),
    ];
    private static readonly Type[] _actionObservables =
    [
        typeof(ActionObservable),
        typeof(ActionObservable<>),
        typeof(ActionObservable<,>),
        typeof(ActionObservable<,,>),
        typeof(ActionObservable<,,,>),
        typeof(ActionObservable<,,,,>),
        typeof(ActionObservable<,,,,,>),
        typeof(ActionObservable<,,,,,,>),
        typeof(ActionObservable<,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,,,,,,,>),
        typeof(ActionObservable<,,,,,,,,,,,,,,,>),
    ];
}
