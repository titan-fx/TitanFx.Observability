using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Reactive.Observability.Expressions;

internal sealed class NullSafeTransform : ExpressionVisitor
{
    private static readonly NullSafeTransform _instance = new();

    [return: NotNullIfNotNull(nameof(node))]
    public static Expression? Apply(Expression? node)
    {
        return _instance.Visit(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        var root = FindCallChain(node, out var chain);
        if (root != node)
            root = Visit(root);
        return Expression.NullPropagate(root, chain, Expression.Default(node.Type), node.Type);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var root = FindCallChain(node, out var chain);
        if (root != node)
            root = Visit(root);
        return Expression.NullPropagate(root, chain, Expression.Default(node.Type), node.Type);
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        var root = FindCallChain(node, out var chain);
        if (root != node)
            root = Visit(root);
        return Expression.NullPropagate(root, chain, Expression.Default(node.Type), node.Type);
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        var root = FindCallChain(node, out var chain);
        if (root != node)
            root = Visit(root);
        return Expression.NullPropagate(root, chain, Expression.Default(node.Type), node.Type);
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        var root = FindCallChain(node, out var chain);
        if (root != node)
            root = Visit(root);
        return Expression.NullPropagate(root, chain, Expression.Default(node.Type), node.Type);
    }

    private static Expression FindCallChain(
        Expression node,
        out ReadOnlySpan<Func<Expression, Expression>> chain
    )
    {
        var funcs = new List<Func<Expression, Expression>>();
        while (true)
        {
            switch (node)
            {
                case MemberExpression { Expression: not null } m:
                    funcs.Add(m.Update);
                    node = m.Expression;
                    break;
                case IndexExpression { Object: not null } i:
                    funcs.Add(x => i.Update(x, i.Arguments));
                    node = i.Object;
                    break;
                case MethodCallExpression { Object: not null } m:
                    funcs.Add(x => m.Update(x, m.Arguments));
                    node = m.Object;
                    break;
                case MethodCallExpression { Object: null, Arguments: [not null, ..] } m
                    when Attribute.IsDefined(m.Method, typeof(ExtensionAttribute)):
                    funcs.Add(x => m.Update(@object: null, [x, .. m.Arguments.Slice(1..)]));
                    node = m.Arguments[0];
                    break;
                case UnaryExpression { Operand: not null, NodeType: ExpressionType.ArrayLength } u:
                    funcs.Add(u.Update);
                    node = u.Operand;
                    break;

                case InvocationExpression { Expression: not null } i:
                    funcs.Add(x => i.Update(x, i.Arguments));
                    node = i.Expression;
                    break;
                default:
                    funcs.Reverse();
                    chain = CollectionsMarshal.AsSpan(funcs);
                    return node;
            }
        }
    }

    private static bool IsNullable(Expression expression)
    {
        var type = expression.Type;
        if (type.IsValueType)
            return Nullable.GetUnderlyingType(type) is not null;

        return !DisplayClass.IsDisplayClass(type);
    }
}
