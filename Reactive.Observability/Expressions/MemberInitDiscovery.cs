using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace Reactive.Observability.Expressions;

internal sealed class MemberInitDiscovery : ExpressionVisitor
{
    public List<MemberAssignment> Assignments { get; } = [];
    public List<MemberMemberBinding> Recursive { get; } = [];
    public List<MemberListBinding> List { get; } = [];
    public List<ElementInit> Elements { get; } = [];

    [return: NotNullIfNotNull(nameof(node))]
    public override Expression? Visit(Expression? node)
    {
        if (node is not MemberInitExpression)
            return node;
        return base.Visit(node);
    }

    protected override MemberAssignment VisitMemberAssignment(MemberAssignment node)
    {
        Assignments.Add(node);
        return base.VisitMemberAssignment(node);
    }

    protected override MemberListBinding VisitMemberListBinding(MemberListBinding node)
    {
        List.Add(node);
        return base.VisitMemberListBinding(node);
    }

    protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding node)
    {
        Recursive.Add(node);
        return base.VisitMemberMemberBinding(node);
    }

    protected override ElementInit VisitElementInit(ElementInit node)
    {
        Elements.Add(node);
        return base.VisitElementInit(node);
    }
}
