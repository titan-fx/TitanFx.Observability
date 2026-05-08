using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace TitanFx.Observability.Expressions;

internal sealed class ExpressionReplacer(Func<Expression, Expression> replacer) : ExpressionVisitor
{
    public ExpressionReplacer(IReadOnlyDictionary<Expression, Expression> mapping)
        : this(v => mapping.GetValueOrDefault(v, v)) { }

    public ExpressionReplacer(Expression target, Expression replacement)
        : this(v => v == target ? replacement : v) { }

    private bool _isRoot = true;

    public bool DirectOnly { get; init; }

    [return: NotNullIfNotNull(nameof(node))]
    public override Expression? Visit(Expression? node)
    {
        if (node is not null)
        {
            var replacement = replacer(node);
            if (!ReferenceEquals(replacement, node))
                return replacement;
        }

        if (!DirectOnly)
            return base.Visit(node);

        if (!_isRoot)
            return node;

        _isRoot = false;
        try
        {
            return base.Visit(node);
        }
        finally
        {
            _isRoot = true;
        }
    }
}
