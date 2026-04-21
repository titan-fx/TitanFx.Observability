using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using System.Reactive;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Reactive.Observability.Expressions;

namespace Reactive.Observability.Observables;

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

    public static ObservableExpression Switch(
        ObservableExpression value,
        ObservableExpression? @default,
        MethodInfo? comparer,
        IEnumerable<KeyValuePair<ImmutableArray<ObservableExpression>, ObservableExpression>> cases
    )
    {
        return SelectSwitch(
            value,
            value =>
                new(
                    Expression.Switch(
                        value,
                        @default,
                        comparer,
                        cases.Select(static x =>
                            Expression.SwitchCase(x.Value, x.Key.Map(static x => x.Value))
                        )
                    )
                )
        );
    }

    public static ObservableExpression Return(Expression value)
    {
        return Collect([], _ => value);
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

    public static ObservableExpression Condition(
        Expression test,
        ObservableExpression ifTrue,
        ObservableExpression ifFalse,
        Type elementType
    )
    {
        return new(
            Expression.Condition(
                test,
                ifTrue,
                ifFalse,
                typeof(IObservable<>).MakeGenericType(elementType)
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
        return new ObservableExpression(Expression.Call(target, watch.Method), typeof(Unit));
    }

    private static readonly MethodInfo _toImmutable = typeof(ImmutableCollectionsMarshal).GetMethod(
        nameof(ImmutableCollectionsMarshal.AsImmutableArray)
    )!;

    private static MethodCallExpression ToExpression(
        Type type,
        ImmutableArray<Expression> expressions
    )
    {
        return Expression.Call(
            instance: null,
            _toImmutable,
            Expression.NewArrayInit(type, expressions)
        );
    }

    public static ObservableExpression TryCatchFaultFinally(
        ObservableExpression bodyObservable,
        IEnumerable<(
            Type ExceptionType,
            ParameterExpression? ExceptionValue,
            ObservableExpression Result
        )> catches,
        ObservableExpression? fault,
        ObservableExpression? @finally
    )
    {
        var catchFilter = typeof(FilteredExceptionObservable<>)
            .MakeGenericType(bodyObservable.ElementType)
            .GetConstructors()[0];
        var error = Expression.Parameter(typeof(Exception));
        var filerType = typeof(Func<Exception, bool>);
        var catchType = TypeLookup.Delegate([typeof(Exception)], bodyObservable.ElementType);
        return new(
            Expression.New(
                typeof(TryCatchFaultFinallyObservable<>)
                    .MakeGenericType(bodyObservable.ElementType)
                    .GetConstructors()[0],
                [
                    bodyObservable,
                    .. catches.Select(x =>
                        Expression.New(
                            catchFilter,
                            [
                                Expression.Lambda(
                                    filerType,
                                    Expression.TypeIs(error, x.ExceptionType),
                                    [error]
                                ),
                                Expression.Lambda(catchType, x.Result, [x.ExceptionValue ?? error]),
                            ]
                        )
                    ),
                    fault?.Value ?? Expression.Constant(null),
                    @finally?.Value ?? Expression.Constant(null),
                ]
            )
        );
    }
}
