using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace Reactive.Observability.Expressions;

internal static class ExprUtil
{
    private static readonly Expression _nullLiteral = Expression.Constant(null);
    private static readonly ConcurrentDictionary<Type, MethodInfo> _getValueOrDefault = [];

    extension(Expression)
    {
        public static Expression IsNull(Expression value)
        {
            if (!value.Type.IsValueType)
                return Expression.ReferenceEqual(value, _nullLiteral);
            if (Nullable.GetUnderlyingType(value.Type) is null)
                return Expression.Constant(false);
            return Expression.Equal(value, Expression.Constant(null, value.Type));
        }

        public static Expression NonNullable(Expression value)
        {
            if (!value.Type.IsValueType)
                return value;
            if (Nullable.GetUnderlyingType(value.Type) is not { } notNull)
                return value;

            return Expression.Call(
                value,
                _getValueOrDefault.GetOrAdd(
                    notNull,
                    static t =>
                        typeof(Nullable<>)
                            .MakeGenericType(t)
                            .GetMethod(nameof(Nullable<>.GetValueOrDefault), [])!
                )
            );
        }

        public static Expression NullPropagate(
            Expression left,
            Func<Expression, Expression> right,
            Expression? ifNull = null,
            Type? returnType = null
        )
        {
            return NullPropagate(left, [right], ifNull, returnType);
        }

        public static Expression NullPropagate(
            Expression left,
            ReadOnlySpan<Func<Expression, Expression>> right,
            Expression? ifNull = null,
            Type? returnType = null
        )
        {
            while (right.Length > 0 && IsNeverNull(left.Type))
            {
                left = right[0](left);
                right = right[1..];
            }

            if (right.Length == 0)
                return left;

            var variables = new List<ParameterExpression>
            {
                left as ParameterExpression ?? Expression.Variable(left.Type),
            };
            var path = new List<Expression> { left };
            for (var i = 0; i < right.Length; i++)
            {
                var next = right[i](variables[^1]);
                if (TryGetNullableMember(next, out var member))
                {
                    if (member.Name is nameof(Nullable<>.Value))
                    {
                        next = NonNullable(variables[^1]);
                    }
                    else
                    {
                        next = right[i](path[^1]);
                        path.RemoveAt(path.Count - 1);
                        variables.RemoveAt(variables.Count - 1);
                    }
                }

                while (IsNeverNull(next.Type) && ++i < right.Length)
                    next = right[i](next);
                path.Add(next);
                variables.Add(Expression.Variable(next.Type));
            }

            variables.RemoveAt(variables.Count - 1);
            ifNull ??= Expression.Default(path[^1].Type);
            returnType ??= ifNull.Type;

            var body = path[^1];
            for (var i = path.Count - 2; i >= 0; i--)
                body = IfNull(variables[i], path[i], ifNull, body, returnType);
            if (variables.Count > 0 && variables[0] == left)
                variables.RemoveAt(0);

            if (variables.Count == 0)
                return body;

            return Expression.Block(returnType, variables, body);

            static bool IsNeverNull(Type type)
            {
                return DisplayClass.IsDisplayClass(type)
                    || (type.IsValueType && Nullable.GetUnderlyingType(type) is null);
            }

            static bool TryGetNullableMember(
                Expression node,
                [MaybeNullWhen(false)] out MemberInfo member
            )
            {
                if (node is MemberExpression m)
                {
                    member = m.Member;
                    return m.Expression is not null
                        && Nullable.GetUnderlyingType(m.Expression.Type) is not null;
                }
                if (node is MethodCallExpression c)
                {
                    member = c.Method;
                    return c.Object is not null
                        && Nullable.GetUnderlyingType(c.Object.Type) is not null;
                }

                member = null;
                return false;
            }

            static Expression IfNull(
                ParameterExpression variable,
                Expression value,
                Expression ifNull,
                Expression ifNonNull,
                Type returnType
            )
            {
                if (value != variable)
                    value = Expression.Assign(variable, value);

                return Expression.Condition(
                    Expression.IsNull(value),
                    ifNull,
                    ifNonNull,
                    returnType
                );
            }
        }
    }
}
