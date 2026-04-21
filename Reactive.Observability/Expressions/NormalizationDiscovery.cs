using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Reactive.Observability.Expressions;

internal sealed class NormalizationDiscovery : ExpressionVisitor
{
    private readonly HashSet<LabelTarget> _labelDedupe = [];
    public List<LabelTarget> Labels { get; } = [];
    private readonly HashSet<ParameterExpression> _parameterDedupe = [];
    public List<ParameterExpression> Parameters { get; } = [];
    public Dictionary<ConstantExpression, Type> Constants { get; } = [];
    private readonly Stack<object> _stack = [];

    private R Using<T, R>(T node, Func<T, R> callBase)
    {
        if (node is null)
            return callBase(node);

        _stack.Push(node);
        try
        {
            return callBase(node);
        }
        finally
        {
            _ = _stack.Pop();
        }
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var type = GetNodeType(node);
        ref var constType = ref CollectionsMarshal.GetValueRefOrAddDefault(
            Constants,
            node,
            out var hasValue
        );
        if (!hasValue || constType!.IsAssignableTo(type))
            constType = type;

        return base.VisitConstant(node);
    }

    [return: NotNullIfNotNull(nameof(node))]
    protected override LabelTarget? VisitLabelTarget(LabelTarget? node)
    {
        if (node is not null && _labelDedupe.Add(node))
            Labels.Add(node);
        return base.VisitLabelTarget(node);
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (_parameterDedupe.Add(node))
            Parameters.Add(node);
        return base.VisitParameter(node);
    }

    protected override Expression VisitDynamic(DynamicExpression node)
    {
        throw new NotSupportedException("Dynamic expressions are not supported");
    }

    protected override Expression VisitExtension(Expression node)
    {
        throw new NotSupportedException("Extension expressions are not supported");
    }

    private static Type GetNodeType(
        ConstantExpression node,
        ReadOnlySpan<Expression?> arguments,
        MethodBase? method
    )
    {
        if (method != null && arguments.IndexOf(node) is { } i && i >= 0)
            return method.GetParameters()[i].ParameterType;

        return node.Type;
    }

    private static Type VoidToAny(Type type) => type == typeof(void) ? typeof(object) : type;

    private Type GetNodeType(ConstantExpression node)
    {
        return VoidToAny(Impl(node));

        Type Impl(ConstantExpression node)
        {
            if (!_stack.TryPop(out var top))
                return node.Type;
            if (!_stack.TryPeek(out var parent))
            {
                _stack.Push(top);
                return node.Type;
            }
            _stack.Push(top);

            switch (parent)
            {
                case BinaryExpression b:
                    return GetNodeType(node, [b.Left, b.Right], b.Method);
                case UnaryExpression u:
                    return GetNodeType(node, [u.Operand], u.Method);
                case MemberExpression m:
                    return m.Expression == node ? m.Member.DeclaringType ?? node.Type : node.Type;
                case IndexExpression i:
                    if (node == i.Object)
                        return i.Indexer?.DeclaringType ?? node.Type;
                    return GetNodeType(node, [.. i.Arguments], i.Indexer?.GetMethod);
                case MethodCallExpression m:
                    if (node == m.Object)
                        return m.Method.DeclaringType ?? node.Type;
                    return GetNodeType(node, [.. m.Arguments], m.Method);
                case NewExpression n:
                    return GetNodeType(node, [.. n.Arguments], n.Constructor);
                case NewArrayExpression n:
                    if (n.NodeType == ExpressionType.NewArrayBounds)
                    {
                        if (n.Expressions.Contains(node))
                            return typeof(int);
                    }
                    else if (n.NodeType == ExpressionType.NewArrayInit)
                    {
                        if (n.Expressions.Contains(node))
                            return n.Type.GetElementType()!;
                    }
                    return node.Type;
                case BlockExpression b:
                    if (b.Expressions[^1] == node)
                        return b.Type;
                    return node.Type;
                case TryExpression t:
                    foreach (var handler in t.Handlers)
                        if (node == handler.Filter)
                            return typeof(bool);
                    if (node == t.Body || node == t.Fault || node == t.Finally)
                        return t.Type;
                    foreach (var handler in t.Handlers)
                        if (node == handler.Body)
                            return t.Type;
                    return node.Type;
                case ConditionalExpression c:
                    if (node == c.Test)
                        return typeof(bool);
                    if (node == c.IfTrue || node == c.IfFalse)
                        return c.Type;
                    return node.Type;
                case ElementInit i:
                    return GetNodeType(node, [.. i.Arguments], i.AddMethod);
                case MemberBinding b:
                    return b.Member switch
                    {
                        PropertyInfo p => p.PropertyType,
                        FieldInfo f => f.FieldType,
                        _ => node.Type,
                    };
                case SwitchExpression s:
                    if (node == s.SwitchValue)
                        return GetNodeType(node, [node, null], s.Comparison);
                    foreach (var @case in s.Cases)
                        if (@case.TestValues.Contains(node))
                            return GetNodeType(node, [null, node], s.Comparison);
                    if (node == s.DefaultBody)
                        return s.Type;
                    foreach (var @case in s.Cases)
                        if (node == @case.Body)
                            return s.Type;
                    return node.Type;
                case TypeBinaryExpression b:
                    if (node == b.Expression)
                        return typeof(object);
                    return node.Type;
                case InvocationExpression i:
                    if (node == i.Expression)
                        return node.Type;
                    return GetNodeType(
                        node,
                        [.. i.Arguments],
                        i.Expression.Type.GetMethod("Invoke")
                    );
                case GotoExpression g:
                    if (node == g.Value)
                        return g.Target.Type;
                    return node.Type;
                case LabelExpression l:
                    if (node == l.DefaultValue)
                        return l.Target.Type;
                    return node.Type;
                case LambdaExpression l:
                    if (node == l.Body)
                        return l.ReturnType;
                    return node.Type;
                case LoopExpression l:
                    if (node == l.Body)
                        return typeof(object);
                    return node.Type;
                default:
                    throw new InvalidOperationException("Unsupported constant expression");
            }
        }
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        return Using(node, base.VisitBinary);
    }

    protected override Expression VisitBlock(BlockExpression node)
    {
        return Using(node, base.VisitBlock);
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        return Using(node, base.VisitConditional);
    }

    protected override ElementInit VisitElementInit(ElementInit node)
    {
        return Using(node, base.VisitElementInit);
    }

    protected override Expression VisitGoto(GotoExpression node)
    {
        return Using(node, base.VisitGoto);
    }

    protected override Expression VisitIndex(IndexExpression node)
    {
        return Using(node, base.VisitIndex);
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        return Using(node, base.VisitInvocation);
    }

    protected override Expression VisitLabel(LabelExpression node)
    {
        return Using(node, base.VisitLabel);
    }

    protected override Expression VisitLambda<T>(Expression<T> node)
    {
        return Using(node, base.VisitLambda);
    }

    protected override Expression VisitLoop(LoopExpression node)
    {
        return Using(node, base.VisitLoop);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        return Using(node, base.VisitMember);
    }

    protected override MemberBinding VisitMemberBinding(MemberBinding node)
    {
        return Using(node, base.VisitMemberBinding);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        return Using(node, base.VisitMethodCall);
    }

    protected override Expression VisitNew(NewExpression node)
    {
        return Using(node, base.VisitNew);
    }

    protected override Expression VisitNewArray(NewArrayExpression node)
    {
        return Using(node, base.VisitNewArray);
    }

    protected override Expression VisitSwitch(SwitchExpression node)
    {
        return Using(node, base.VisitSwitch);
    }

    protected override Expression VisitTry(TryExpression node)
    {
        return Using(node, base.VisitTry);
    }

    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
    {
        return Using(node, base.VisitTypeBinary);
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        return Using(node, base.VisitUnary);
    }
}
