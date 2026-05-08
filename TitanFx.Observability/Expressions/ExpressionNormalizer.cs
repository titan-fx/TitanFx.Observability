using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TitanFx.Observability.Expressions;

internal sealed class ExpressionNormalizer : ExpressionVisitor
{
    private static readonly ConcurrentDictionary<object, object> _dedupe = new(
        new ExpressionEqualityComparer()
    );
    private static readonly ConcurrentDictionary<Type, ParameterExpression> _rootParameters = [];
    private static readonly ConcurrentDictionary<ParameterExpression, Expression[]> _tupleFields =
        new(ReferenceEqualityComparer.Instance);
    private readonly IReadOnlyDictionary<ConstantExpression, Expression> _constants;
    private readonly IReadOnlyDictionary<LabelTarget, int> _labelIndexes;
    private readonly IReadOnlyDictionary<ParameterExpression, int> _parameterIndexes;

    public static T Normalize<T>(
        T expression,
        out ParameterExpression parameter,
        out ITuple constants
    )
        where T : Expression
    {
        var discover = new NormalizationDiscovery();
        _ = discover.Visit(expression);
        var entries = discover
            .Constants.GroupBy(
                static kvp => (kvp.Value, kvp.Key.Value),
                static kvp => kvp.Key,
                static (k, x) => (Type: k.Item1, Expressions: x, Value: k.Item2),
                DisplayClassEqualityComparer.Instance
            )
            .ToList();
        var tupleInfo = ValueTupleUtil.Create(
            CollectionsMarshal.AsSpan(entries).Map(static kvp => (kvp.Type, kvp.Value))
        );
        parameter = _rootParameters.GetOrAdd(
            tupleInfo.Type,
            static type =>
            {
                var elements = ValueTupleUtil.Elements(type);
                return Expression.Parameter(type, "ctx");
            }
        );
        constants = tupleInfo.Value;
        var replacements = _tupleFields.GetOrAdd(parameter, ValueTupleUtil.Fields);

        var deduper = new ExpressionNormalizer(
            constants: entries
                .Zip(replacements)
                .SelectMany(
                    x => x.First.Expressions,
                    (x, target) => (target, replacement: x.Second)
                )
                .ToDictionary(static x => x.target, static x => x.replacement),
            labels: discover.Labels.IndexBy(static x => x.Type).ToDictionary(),
            parameters: discover.Parameters.IndexBy(static x => x.Type).ToDictionary()
        );
        return (T)deduper.Visit(expression);
    }

    private ExpressionNormalizer(
        IReadOnlyDictionary<ConstantExpression, Expression> constants,
        IReadOnlyDictionary<LabelTarget, int> labels,
        IReadOnlyDictionary<ParameterExpression, int> parameters
    )
    {
        _constants = constants;
        _labelIndexes = labels;
        _parameterIndexes = parameters;
    }

    [return: NotNullIfNotNull(nameof(node))]
    public override Expression? Visit(Expression? node)
    {
        if (node is null)
            return null;

        var result = base.Visit(node);
        if (result is ConstantExpression or ParameterExpression)
            return result;

        return Dedupe(result);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (_constants.TryGetValue(node, out var replacement))
            return replacement;
        return node;
    }

    private static readonly ConcurrentDictionary<Type, List<ParameterExpression>> _parameters = [];

    protected override ParameterExpression VisitParameter(ParameterExpression node)
    {
        var id = _parameterIndexes[node];
        var cache = _parameters.GetOrAdd(node.Type, _ => []);
        while (cache.Count <= id)
            cache.AddRange(Enumerable.Range(0, 10).Select(i => Expression.Parameter(node.Type)));
        return cache[id];
    }

    private static readonly ConcurrentDictionary<Type, List<LabelTarget>> _labels = [];

    [return: NotNullIfNotNull(nameof(node))]
    protected override LabelTarget? VisitLabelTarget(LabelTarget? node)
    {
        if (node is null)
            return null;

        var id = _labelIndexes[node];
        var cache = _labels.GetOrAdd(node.Type, _ => []);
        while (cache.Count <= id)
            cache.AddRange(Enumerable.Range(0, 10).Select(i => Expression.Label(node.Type)));
        return cache[id];
    }

    private static T Dedupe<T>(T value)
        where T : class => Unsafe.As<T>(_dedupe.GetOrAdd(value, value));
}

file sealed class DisplayClassEqualityComparer : IEqualityComparer<(Type, object?)>
{
    public static DisplayClassEqualityComparer Instance { get; } = new();

    private DisplayClassEqualityComparer() { }

    public bool Equals((Type, object?) x, (Type, object?) y)
    {
        if (x.Item1 != y.Item1 || x.Item2 != y.Item2)
            return false;

        return DisplayClass.IsDisplayClass(x.Item1);
    }

    public int GetHashCode([DisallowNull] (Type, object?) obj)
    {
        return obj.Item1.GetHashCode();
    }
}

file static class Ext
{
    public static IEnumerable<KeyValuePair<T, int>> IndexBy<T, TKey>(
        this IEnumerable<T> source,
        Func<T, TKey> selector
    )
    {
        return source
            .GroupBy(selector, resultSelector: static (_, x) => x.Select(KeyValuePair.Create))
            .SelectMany(x => x);
    }
}
