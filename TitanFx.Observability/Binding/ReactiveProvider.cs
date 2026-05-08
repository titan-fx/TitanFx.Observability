using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using TitanFx.Observability.Expressions;

namespace TitanFx.Observability.Binding;

public sealed class ReactiveProvider(IReactiveBinder binder)
{
    private static readonly ConcurrentDictionary<Expression, Delegate> _binders = new(
        ReferenceEqualityComparer.Instance
    );

    /// <summary>
    /// Converts the expression tree into a delegate which can be called with the same arguments.
    /// The return value will be an <![CDATA[IObservable<TResult?>]]> instead of a TResult.
    /// e.g. an <![CDATA[Expression<Func<string>>]]> will return a <![CDATA[Func<IObservable<string?>>]]>
    /// </summary>
    /// <typeparam name="TDelegate"></typeparam>
    /// <param name="expression"></param>
    /// <returns></returns>
    internal Delegate Build<TDelegate>(Expression<TDelegate> expression)
        where TDelegate : Delegate
    {
        expression = ExpressionNormalizer.Normalize(expression, out var context, out var constants);
        var binderFn = _binders.GetOrAdd(
            expression.Body,
            Compile,
            (context, expression.Parameters)
        );
        return Unsafe.As<Delegate>(binderFn.DynamicInvoke(constants)!);
    }

    private Delegate Compile(
        Expression body,
        (ParameterExpression Context, ReadOnlyCollection<ParameterExpression> Parameters) arg
    )
    {
        var observable = ReactiveRewriter.Rewrite(body, binder, [arg.Context, .. arg.Parameters]);
        var boundLambda = Expression.Lambda(
            TypeLookup.Delegate(arg.Parameters.Map(static x => x.Type), observable.Type),
            observable,
            [.. arg.Parameters]
        );
        var lambda = Expression.Lambda(
            TypeLookup.Delegate([arg.Context.Type], boundLambda.Type),
            boundLambda,
            [arg.Context]
        );
        return lambda.Compile();
    }
}
