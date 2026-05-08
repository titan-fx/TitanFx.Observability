using System;
using System.Linq.Expressions;
using TitanFx.Observability.Expressions;

namespace TitanFx.Observability.Observables;

internal readonly struct ObservableExpression
{
    public Expression Value { get; }
    public Type ElementType { get; }
    public Type Type => typeof(IObservable<>).MakeGenericType(ElementType);

    public ObservableExpression(Expression value, Type elementType)
    {
        if (!value.Type.IsAssignableTo(typeof(IObservable<>).MakeGenericType(elementType)))
            throw new ArgumentException(
                "Expression does not implement IObservable<>",
                nameof(elementType)
            );

        Value = value;
        ElementType = elementType;
    }

    public ObservableExpression(Expression value)
        : this(value, GetElementType(value.Type)) { }

    private static Type GetElementType(Type observable)
    {
        if (
            observable.IsGenericType
            && observable.GetGenericTypeDefinition() == typeof(IObservable<>)
        )
            return observable.GetGenericArguments()[0];

        Type? match = null;
        foreach (var iType in observable.GetInterfaces())
        {
            if (iType.IsGenericType && iType.GetGenericTypeDefinition() == typeof(IObservable<>))
            {
                if (match is not null)
                    throw new ArgumentException(
                        "Type implements multiple IObservables",
                        nameof(observable)
                    );
                match = iType.GetGenericArguments()[0];
            }
        }

        return match
            ?? throw new ArgumentException(
                "Type does not implement IObservable",
                nameof(observable)
            );
    }

    public ObservableExpression Replace(Expression target, Expression replacement)
    {
        return new(new ExpressionReplacer(target, replacement).Visit(Value), Type);
    }

    public static implicit operator Expression?(ObservableExpression? value) => value?.Value;

    public static implicit operator Expression(ObservableExpression value) => value.Value;

    public static explicit operator ObservableExpression(Expression value) => new(value);
}
