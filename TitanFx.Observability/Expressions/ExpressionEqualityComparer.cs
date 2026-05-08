using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace TitanFx.Observability.Expressions;

internal sealed class ExpressionEqualityComparer : IEqualityComparer<object>
{
    private static readonly IEqualityComparer<object?> _byRef = ReferenceEqualityComparer.Instance;

    public new bool Equals(object? x, object? y)
    {
        if (ReferenceEquals(x, y))
            return true;
        if (x is null || y is null)
            return false;

        switch ((x, y))
        {
            case (Expression a, Expression b):
                if (a.Type != b.Type || a.NodeType != b.NodeType)
                    return false;
                break;
            case (Expression, _):
            case (_, Expression):
                return false;
        }

        return (x, y) switch
        {
            (BinaryExpression a, BinaryExpression b) => a.Method == b.Method
                && ReferenceEquals(a.Left, b.Left)
                && ReferenceEquals(a.Right, b.Right),
            (BlockExpression a, BlockExpression b) => a.Variables.SequenceEqual(b.Variables, _byRef)
                && a.Expressions.SequenceEqual(b.Expressions, _byRef),
            (CatchBlock a, CatchBlock b) => a.Test == b.Test
                && ReferenceEquals(a.Variable, b.Variable)
                && ReferenceEquals(a.Filter, b.Filter)
                && ReferenceEquals(a.Body, b.Body),
            (ConditionalExpression a, ConditionalExpression b) => ReferenceEquals(a.Test, b.Test)
                && ReferenceEquals(a.IfTrue, b.IfTrue)
                && ReferenceEquals(a.IfFalse, b.IfFalse),
            (ConstantExpression a, ConstantExpression b) => Equals(a.Value, b.Value),
            (DefaultExpression, DefaultExpression) => true,
            (ElementInit a, ElementInit b) => a.AddMethod == b.AddMethod
                && a.Arguments.SequenceEqual(b.Arguments, _byRef),
            (GotoExpression a, GotoExpression b) => a.Kind == b.Kind
                && ReferenceEquals(a.Target, b.Target)
                && ReferenceEquals(a.Value, b.Value),
            (IndexExpression a, IndexExpression b) => a.Indexer == b.Indexer
                && ReferenceEquals(a.Object, b.Object)
                && a.Arguments.SequenceEqual(b.Arguments, _byRef),
            (InvocationExpression a, InvocationExpression b) => ReferenceEquals(
                a.Expression,
                b.Expression
            ) && a.Arguments.SequenceEqual(b.Arguments, _byRef),
            (LabelExpression a, LabelExpression b) => ReferenceEquals(a.Target, b.Target)
                && ReferenceEquals(a.DefaultValue, b.DefaultValue),
            (LambdaExpression a, LambdaExpression b) => a.ReturnType == b.ReturnType
                && ReferenceEquals(a.Body, b.Body)
                && a.Parameters.SequenceEqual(b.Parameters, _byRef),
            (ListInitExpression a, ListInitExpression b) => ReferenceEquals(
                a.NewExpression,
                b.NewExpression
            ) && a.Initializers.SequenceEqual(b.Initializers, _byRef),
            (LoopExpression a, LoopExpression b) => ReferenceEquals(
                a.ContinueLabel,
                b.ContinueLabel
            )
                && ReferenceEquals(a.BreakLabel, b.BreakLabel)
                && ReferenceEquals(a.Body, b.Body),
            (MemberExpression a, MemberExpression b) => a.Member == b.Member
                && ReferenceEquals(a.Expression, b.Expression),
            (MemberAssignment a, MemberAssignment b) => ReferenceEquals(a.Expression, b.Expression),
            (MemberMemberBinding a, MemberMemberBinding b) => a.Bindings.SequenceEqual(
                b.Bindings,
                _byRef
            ),
            (MemberListBinding a, MemberListBinding b) => a.Initializers.SequenceEqual(
                b.Initializers,
                _byRef
            ),
            (MemberInitExpression a, MemberInitExpression b) => ReferenceEquals(
                a.NewExpression,
                b.NewExpression
            ) && a.Bindings.SequenceEqual(b.Bindings, _byRef),
            (MethodCallExpression a, MethodCallExpression b) => a.Method == b.Method
                && ReferenceEquals(a.Object, b.Object)
                && a.Arguments.SequenceEqual(b.Arguments, _byRef),
            (NewExpression a, NewExpression b) => a.Constructor == b.Constructor
                && a.Arguments.SequenceEqual(b.Arguments, _byRef),
            (NewArrayExpression a, NewArrayExpression b) => a.Expressions.SequenceEqual(
                b.Expressions,
                _byRef
            ),
            (RuntimeVariablesExpression a, RuntimeVariablesExpression b) =>
                a.Variables.SequenceEqual(b.Variables, _byRef),
            (SwitchExpression a, SwitchExpression b) => ReferenceEquals(
                a.SwitchValue,
                b.SwitchValue
            )
                && a.Comparison == b.Comparison
                && a.Cases.SequenceEqual(b.Cases, _byRef)
                && ReferenceEquals(a.DefaultBody, b.DefaultBody),
            (SwitchCase a, SwitchCase b) => a.TestValues.SequenceEqual(b.TestValues, _byRef)
                && ReferenceEquals(a.Body, b.Body),
            (TryExpression a, TryExpression b) => ReferenceEquals(a.Body, b.Body)
                && ReferenceEquals(a.Fault, b.Fault)
                && ReferenceEquals(a.Finally, b.Finally)
                && a.Handlers.SequenceEqual(b.Handlers, _byRef),
            (TypeBinaryExpression a, TypeBinaryExpression b) => a.TypeOperand == b.TypeOperand
                && ReferenceEquals(a.Expression, b.Expression),
            (UnaryExpression a, UnaryExpression b) => a.Method == b.Method
                && ReferenceEquals(a.Operand, b.Operand),
            _ => false,
        };
    }

    public int GetHashCode([DisallowNull] object obj)
    {
        var hc = new HashCode();
        if (obj is Expression x1)
        {
            hc.Add(x1.Type);
            hc.Add(x1.NodeType);
        }
        else if (obj is MemberBinding x2)
        {
            hc.Add(x2.BindingType);
            hc.Add(x2.Member);
        }

        switch (obj)
        {
            case BinaryExpression b:
                hc.Add(b.Method);
                hc.Add(b.Left, _byRef);
                hc.Add(b.Right, _byRef);
                break;
            case BlockExpression b:
                AddEach(hc, b.Variables, _byRef);
                AddEach(hc, b.Expressions, _byRef);
                break;
            case CatchBlock c:
                hc.Add(c.Test);
                hc.Add(c.Variable, _byRef);
                hc.Add(c.Filter, _byRef);
                hc.Add(c.Body, _byRef);
                break;
            case ConditionalExpression c:
                hc.Add(c.Test, _byRef);
                hc.Add(c.IfTrue, _byRef);
                hc.Add(c.IfFalse, _byRef);
                break;
            case ConstantExpression c:
                hc.Add(c.Value);
                break;
            case DefaultExpression:
                break;
            case ElementInit e:
                hc.Add(e.AddMethod);
                AddEach(hc, e.Arguments, _byRef);
                break;
            case GotoExpression g:
                hc.Add(g.Kind);
                hc.Add(g.Target, _byRef);
                hc.Add(g.Value, _byRef);
                break;
            case IndexExpression i:
                hc.Add(i.Indexer);
                hc.Add(i.Object, _byRef);
                AddEach(hc, i.Arguments, _byRef);
                break;
            case InvocationExpression i:
                hc.Add(i.Expression, _byRef);
                AddEach(hc, i.Arguments, _byRef);
                break;
            case LabelExpression l:
                hc.Add(l.Target, _byRef);
                hc.Add(l.DefaultValue, _byRef);
                break;
            case LambdaExpression l:
                hc.Add(l.ReturnType);
                hc.Add(l.Body, _byRef);
                AddEach(hc, l.Parameters, _byRef);
                break;
            case ListInitExpression l:
                hc.Add(l.NewExpression, _byRef);
                AddEach(hc, l.Initializers, _byRef);
                break;
            case LoopExpression l:
                hc.Add(l.ContinueLabel, _byRef);
                hc.Add(l.BreakLabel, _byRef);
                hc.Add(l.Body, _byRef);
                break;
            case MemberExpression m:
                hc.Add(m.Member);
                hc.Add(m.Expression, _byRef);
                break;
            case MemberAssignment m:
                hc.Add(m.Expression, _byRef);
                break;
            case MemberMemberBinding m:
                AddEach(hc, m.Bindings, _byRef);
                break;
            case MemberListBinding m:
                AddEach(hc, m.Initializers, _byRef);
                break;
            case MemberInitExpression m:
                hc.Add(m.NewExpression, _byRef);
                AddEach(hc, m.Bindings, _byRef);
                break;
            case MethodCallExpression m:
                hc.Add(m.Method);
                hc.Add(m.Object, _byRef);
                AddEach(hc, m.Arguments, _byRef);
                break;
            case NewExpression n:
                hc.Add(n.Constructor);
                AddEach(hc, n.Arguments, _byRef);
                break;
            case NewArrayExpression n:
                AddEach(hc, n.Expressions, _byRef);
                break;
            case RuntimeVariablesExpression r:
                AddEach(hc, r.Variables, _byRef);
                break;
            case SwitchExpression s:
                hc.Add(s.SwitchValue, _byRef);
                hc.Add(s.Comparison);
                AddEach(hc, s.Cases, _byRef);
                hc.Add(s.DefaultBody, _byRef);
                break;
            case SwitchCase s:
                AddEach(hc, s.TestValues, _byRef);
                hc.Add(s.Body, _byRef);
                break;
            case TryExpression t:
                hc.Add(t.Body, _byRef);
                hc.Add(t.Fault, _byRef);
                hc.Add(t.Finally, _byRef);
                AddEach(hc, t.Handlers, _byRef);
                break;
            case TypeBinaryExpression t:
                hc.Add(t.TypeOperand);
                hc.Add(t.Expression, _byRef);
                break;
            case UnaryExpression u:
                hc.Add(u.Method);
                hc.Add(u.Operand, _byRef);
                break;

            default:
                throw new ArgumentException("Unsupported type", nameof(obj));
        }
        return hc.ToHashCode();
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "Add")]
    private static extern void AddRaw(in HashCode hc, int value);

    private static void AddEach<U>(
        in HashCode hc,
        IEnumerable<U> source,
        IEqualityComparer<U> comparer
    )
    {
        foreach (var item in source)
            hc.Add(item, comparer);
    }
}
