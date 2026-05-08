using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using TitanFx.Observability.Expressions;

namespace TitanFx.Observability.Observables;

internal static class ObservableExpressions
{
    public static ObservableExpression SelectSwitch(
        ObservableExpression observable,
        Func<ParameterExpression, ObservableExpression> selector
    )
    {
        var param = Expression.Parameter(observable.ElementType);
        var body = new ObservableExpression(selector(param));

        return new(
            Expression.New(
                typeof(SelectSwitchObservable<,>)
                    .MakeGenericType(param.Type, body.ElementType)
                    .GetConstructors()[0],
                [
                    observable,
                    Expression.Lambda(
                        TypeLookup.Delegate(
                            [param.Type],
                            typeof(IObservable<>).MakeGenericType(body.ElementType)
                        ),
                        body,
                        [param]
                    ),
                ]
            )
        );
    }

    public static ObservableExpression Collect(
        ReadOnlySpan<ObservableExpression> observables,
        Func<ReadOnlySpan<Expression>, Expression> makeBody
    )
    {
        if (observables.Length <= TypeLookup.MaxArguments)
            return Create(observables, p => makeBody(p));

        var workspace = observables.ToArray().AsSpan();
        var tupleSize = TypeLookup.MaxValueTuple - 1;
        while (workspace.Length > TypeLookup.MaxArguments)
        {
            var tail = workspace[^tupleSize..];
            var tailType = TypeLookup.ValueTuple(tail.Map(static x => x.ElementType));
            var tailObservable = Create(
                tail,
                p => Expression.New(tailType.GetConstructors()[0], [.. p])
            );
            workspace[^tupleSize] = tailObservable;
            workspace = workspace[..^(tupleSize - 1)];
            tupleSize = TypeLookup.MaxValueTuple;
        }

        return Create(workspace, p => makeBody([.. p[..^1], .. ValueTupleUtil.Fields(p[^1])]));
    }

    private static ObservableExpression Create(
        ReadOnlySpan<ObservableExpression> observables,
        Func<ReadOnlySpan<ParameterExpression>, Expression> makeBody
    )
    {
        var argTypes = observables.Map(static x => x.ElementType);
        var parameters = argTypes.Map(Expression.Parameter);
        var body = makeBody(parameters);
        return new(
            Expression.New(
                TypeLookup.CombineObservable(argTypes, body.Type).GetConstructors()[0],
                [
                    .. observables,
                    Expression.Lambda(TypeLookup.Delegate(argTypes, body.Type), body, parameters),
                ]
            )
        );
    }

    public static ObservableExpression Watch(Delegate watch, ObservableExpression instance)
    {
        var target = watch.Target is null ? null : Expression.Constant(watch.Target);
        return SelectSwitch(
            instance,
            p =>
                new(
                    Expression.NullPropagate(
                        p,
                        p => Expression.Call(target, watch.Method, [p]),
                        Expression.Constant(DefaultObservable.Get(instance.ElementType)),
                        instance.Type
                    )
                )
        );
    }

    public static ObservableExpression Watch(Delegate watch)
    {
        var target = watch.Target is null ? null : Expression.Constant(watch.Target);
        return new ObservableExpression(Expression.Call(target, watch.Method), typeof(Nothing));
    }

    private static readonly MethodInfo _toImmutable = typeof(ImmutableCollectionsMarshal).GetMethod(
        nameof(ImmutableCollectionsMarshal.AsImmutableArray)
    )!;
}
