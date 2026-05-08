using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using TitanFx.Observability.Binding;
using TitanFx.Observability.Observables;

namespace TitanFx.Observability.Expressions;

internal sealed class ReactiveRewriter
    : ExpressionTranslator<ReactiveRewriter.Result, ReactiveRewriter.State>
{
    private static readonly ReactiveRewriter _instance = new();

    private ReactiveRewriter() { }

    public static ObservableExpression Rewrite(
        Expression body,
        IReactiveBinder binder,
        ImmutableArray<ParameterExpression> parameters
    )
    {
        var state = new State(binder, parameters);
        var split = _instance.Translate(body, state);
        return ToObservable(split, state);
    }

    public readonly record struct Result(
        Expression Expression,
        ReadOnlyCollection<ParameterExpression> Dependencies
    );

    public readonly record struct State(
        IReactiveBinder Binder,
        ImmutableArray<ParameterExpression> Roots
    )
    {
        public Dictionary<ParameterExpression, IObservableSource> Parameters { get; } =
            new(ReferenceEqualityComparer.Instance);
        public Dictionary<Expression, Result> Results { get; } =
            new(ReferenceEqualityComparer.Instance);
    }

    public interface IObservableSource
    {
        ObservableExpression ToObservable(in State state);
    }

    private sealed class Watchable(Result? instance, Delegate watch) : IObservableSource
    {
        public ObservableExpression ToObservable(in State state)
        {
            return instance is { } i
                ? ObservableExpressions.Watch(watch, ReactiveRewriter.ToObservable(i, state))
                : ObservableExpressions.Watch(watch);
        }
    }

    private sealed class Conditional(
        Result test,
        ParameterExpression cache,
        Expression condition,
        Result ifTrue,
        Result ifFalse,
        Type returnType
    ) : IObservableSource
    {
        public ObservableExpression ToObservable(in State state)
        {
            var resultFnType = TypeLookup.Delegate(
                [cache.Type],
                typeof(IObservable<>).MakeGenericType(returnType)
            );

            return new(
                Expression.New(
                    typeof(ConditionalObservable<,>)
                        .MakeGenericType(cache.Type, returnType)
                        .GetConstructors()[0],
                    [
                        ReactiveRewriter.ToObservable(test, state),
                        Expression.Lambda(
                            TypeLookup.Delegate([cache.Type], typeof(bool)),
                            condition,
                            [cache]
                        ),
                        Expression.Lambda(
                            resultFnType,
                            ReactiveRewriter.ToObservable(ifTrue, state),
                            [cache]
                        ),
                        Expression.Lambda(
                            resultFnType,
                            ReactiveRewriter.ToObservable(ifFalse, state),
                            [cache]
                        ),
                    ]
                )
            );
        }
    }

    private static ObservableExpression ToObservable(Result result, in State state)
    {
        var dependencies = new ObservableExpression[result.Dependencies.Count];
        for (var i = 0; i < dependencies.Length; i++)
            dependencies[i] = state.Parameters[result.Dependencies[i]].ToObservable(state);

        if (dependencies.Length == 1 && result.Expression == result.Dependencies[0])
            return dependencies[0];

        return ObservableExpressions.Collect(
            dependencies,
            p =>
            {
                var mapping = new Dictionary<Expression, Expression>(
                    p.Length,
                    ReferenceEqualityComparer.Instance
                );
                for (var i = 0; i < p.Length; i++)
                    mapping[result.Dependencies[i]] = p[i];
                var replacer = new ExpressionReplacer(mapping);
                return NullSafeTransform.Apply(replacer.Visit(result.Expression));
            }
        );
    }

    public override Result Translate(Expression node, in State state)
    {
        if (state.Results.TryGetValue(node, out var result))
            return result;
        return state.Results[node] = base.Translate(node, state);
    }

    protected override Result Parameter(ParameterExpression node, in State state)
    {
        if (state.Roots.Contains(node))
            return new(node, []);
        return new(node, [node]);
    }

    protected override Result Constant(ConstantExpression node, in State state)
    {
        return new(node, []);
    }

    protected override Result Default(DefaultExpression node, in State state)
    {
        return new(node, []);
    }

    protected override Result Add(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result AddX(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result And(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result Or(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result XOr(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result ArrayIndex(BinaryExpression node, in State state) =>
        Simple(node, state);

    protected override Result Div(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result ShiftL(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result ShiftR(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result Mod(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result Pow(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result Mul(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result MulX(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result Sub(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result SubX(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result Eq(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result NE(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result GE(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result GT(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result LE(BinaryExpression node, in State state) => Simple(node, state);

    protected override Result LT(BinaryExpression node, in State state) => Simple(node, state);

    private static Result Simple(BinaryExpression node, in State state)
    {
        var mapper = new ResultMap([node.Left, node.Right], state);
        return new(mapper.Replace(node), mapper.AllDependencies);
    }

    protected override Result Dec(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result Inc(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result Unbox(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result Plus(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result As(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result Convert(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result ConvertX(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result ArrayLength(UnaryExpression node, in State state) =>
        Simple(node, state);

    protected override Result Invert(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result IsTrue(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result IsFalse(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result Not(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result Negate(UnaryExpression node, in State state) => Simple(node, state);

    protected override Result NegateX(UnaryExpression node, in State state) => Simple(node, state);

    private static Result Simple(UnaryExpression node, in State state)
    {
        var mapper = new ResultMap([node.Operand], state);
        return new(mapper.Replace(node), mapper.AllDependencies);
    }

    protected override Result Coalesce(BinaryExpression node, in State state)
    {
        var mapping = new ResultMap([node.Left, node.Conversion, node.Right], state);

        var param = Expression.Parameter(node.Type);
        var cache = Expression.Parameter(node.Left.Type);

        if (ContainsLocal(mapping.AllDependencies, state))
            return new(mapping.Replace(node), mapping.AllDependencies);

        Result nonNull;
        if (node.Conversion is null)
        {
            nonNull = new(Expression.NonNullable(cache), []);
        }
        else
        {
            var convertArg = node.Conversion.Parameters[0].Type.IsAssignableFrom(cache.Type)
                ? cache
                : Expression.NonNullable(cache);
            var convert = mapping.Get(node.Conversion);
            nonNull = new(Expression.Invoke(convert.Expression, convertArg), convert.Dependencies);
        }

        state.Parameters[param] = new Conditional(
            mapping.Get(node.Left),
            cache,
            Expression.IsNull(cache),
            mapping.Get(node.Right),
            nonNull,
            node.Type
        );
        return new(param, [param]);
    }

    protected override Result OrElse(BinaryExpression node, in State state)
    {
        var mapping = new ResultMap([node.Left, node.Right], state);
        if (ContainsLocal(mapping.AllDependencies, state))
            return new(mapping.Replace(node), mapping.AllDependencies);

        var param = Expression.Parameter(node.Type);
        var cache = Expression.Parameter(node.Left.Type);

        state.Parameters[param] = new Conditional(
            mapping.Get(node.Left),
            cache,
            cache,
            new(cache, []),
            mapping.Get(node.Right),
            node.Type
        );
        return new(param, [param]);
    }

    protected override Result AndAlso(BinaryExpression node, in State state)
    {
        var mapping = new ResultMap([node.Left, node.Right], state);
        if (ContainsLocal(mapping.AllDependencies, state))
            return new(mapping.Replace(node), mapping.AllDependencies);

        var param = Expression.Parameter(node.Type);
        var cache = Expression.Parameter(node.Left.Type);

        state.Parameters[param] = new Conditional(
            mapping.Get(node.Left),
            cache,
            cache,
            mapping.Get(node.Right),
            new(cache, []),
            node.Type
        );
        return new(param, [param]);
    }

    protected override Result Ternary(ConditionalExpression node, in State state)
    {
        var mapping = new ResultMap([node.Test, node.IfTrue, node.IfFalse], state);
        if (ContainsLocal(mapping.AllDependencies, state))
            return new(mapping.Replace(node), mapping.AllDependencies);

        var param = Expression.Parameter(node.Type);
        var cache = Expression.Parameter(node.Test.Type);

        state.Parameters[param] = new Conditional(
            mapping.Get(node.Test),
            cache,
            cache,
            mapping.Get(node.IfTrue),
            mapping.Get(node.IfFalse),
            node.Type
        );
        return new(param, [param]);
    }

    private static bool ContainsLocal(IEnumerable<ParameterExpression> dependencies, in State state)
    {
        return dependencies.Except(state.Parameters.Keys).Any();
    }

    protected override Result NewParameterless(NewExpression node, in State state)
    {
        return new(node, []);
    }

    protected override Result New(NewExpression node, ConstructorInfo constructor, in State state)
    {
        var mapping = new ResultMap(node.Arguments, state);
        return new(mapping.Replace(node), mapping.AllDependencies);
    }

    protected override Result MemberInit(MemberInitExpression node, in State state)
    {
        return Init(node, node.NewExpression, BindingValues(node.Bindings), state);
        static IEnumerable<Expression> BindingValues(ReadOnlyCollection<MemberBinding> bindings)
        {
            return bindings.SelectMany(b =>
                b.BindingType switch
                {
                    MemberBindingType.Assignment => [Unsafe.As<MemberAssignment>(b).Expression],
                    MemberBindingType.MemberBinding => BindingValues(
                        Unsafe.As<MemberMemberBinding>(b).Bindings
                    ),
                    MemberBindingType.ListBinding => Unsafe
                        .As<MemberListBinding>(b)
                        .Initializers.SelectMany(static i => i.Arguments),
                    _ => throw new UnreachableException(),
                }
            );
        }
    }

    protected override Result ListInit(ListInitExpression node, in State state)
    {
        return Init(
            node,
            node.NewExpression,
            node.Initializers.SelectMany(static i => i.Arguments),
            state
        );
    }

    protected override Result NewArray(NewArrayExpression node, in State state)
    {
        var mapping = new ResultMap(node.Expressions, state);
        return new(mapping.Replace(node), mapping.AllDependencies);
    }

    protected override Result NewArraySize(NewArrayExpression node, in State state)
    {
        var mapping = new ResultMap(node.Expressions, state);
        return new(mapping.Replace(node), mapping.AllDependencies);
    }

    private static Result Init(
        Expression node,
        NewExpression @new,
        IEnumerable<Expression> memberValues,
        in State state
    )
    {
        var mapper = new ResultMap([@new, .. memberValues], state);
        return new(mapper.Replace(node), mapper.AllDependencies);
    }

    protected override Result Member(MemberExpression node, in State state)
    {
        return TryWatch(node, node.Expression, node.Member, [], state);
    }

    protected override Result Index(IndexExpression node, PropertyInfo indexer, in State state)
    {
        return TryWatch(node, node.Object, node.Indexer, node.Arguments, state);
    }

    protected override Result Index(IndexExpression node, in State state)
    {
        var mapping = new ResultMap([node.Object, .. node.Arguments], state);
        return new(mapping.Replace(node), mapping.AllDependencies);
    }

    protected override Result Call(MethodCallExpression node, in State state)
    {
        if (!Attribute.IsDefined(node.Method, typeof(ExtensionAttribute)))
            return TryWatch(node, node.Object, node.Method, node.Arguments, state);

        return TryWatch(node, node.Arguments[0], node.Method, node.Arguments.Slice(1..), state);
    }

    private Result TryWatch(
        Expression node,
        Expression? instance,
        MemberInfo? member,
        ReadOnlyCollection<Expression> arguments,
        in State state
    )
    {
        if (member is null || state.Binder.Watch(instance?.Type, member) is not { } watch)
        {
            var mapping = new ResultMap([instance, .. arguments], state);
            return new(mapping.Replace(node), mapping.AllDependencies);
        }
        else
        {
            var param = Expression.Parameter(instance?.Type ?? typeof(Nothing));
            var @this = TranslateNullable(instance, state);
            var mapping = new ResultMap(arguments, state);
            if (
                (@this.TryGetValue(out var v) && ContainsLocal(v.Dependencies, state))
                || ContainsLocal(mapping.AllDependencies, state)
            )
            {
                return new(mapping.Replace(node), mapping.AllDependencies);
            }

            if (instance is not null)
                node = new ExpressionReplacer(instance, param).Visit(node);

            state.Parameters[param] = new Watchable(@this.ToNullable(), watch);
            return new(mapping.Replace(node), [param, .. mapping.AllDependencies]);
        }
    }

    protected override Result Invoke(InvocationExpression node, in State state)
    {
        var mapper = new ResultMap([node.Expression, .. node.Arguments], state);
        return new(mapper.Replace(node), mapper.AllDependencies);
    }

    protected override Result Lambda<T>(Expression<T> node, in State state)
    {
        var mapper = new ResultMap([node.Body], state);
        return new(mapper.Replace(node), [.. mapper.AllDependencies.Except(node.Parameters)]);
    }

    protected override Result Quote(UnaryExpression node, in State state)
    {
        var mapper = new ResultMap([node.Operand], state);
        return new(mapper.Replace(node), mapper.AllDependencies);
    }

    protected override Result TypeEq(TypeBinaryExpression node, in State state)
    {
        var mapper = new ResultMap([node.Expression], state);
        return new(mapper.Replace(node), mapper.AllDependencies);
    }

    protected override Result TypeIs(TypeBinaryExpression node, in State state)
    {
        var mapper = new ResultMap([node.Expression], state);
        return new(mapper.Replace(node), mapper.AllDependencies);
    }

    protected override Result Block(BlockExpression node, in State state)
    {
        var mapping = new ResultMap(node.Expressions, state);
        return new(mapping.Replace(node), [.. mapping.AllDependencies.Except(node.Variables)]);
    }

    protected override Result DebugInfo(DebugInfoExpression node, in State state)
    {
        return new(node, []);
    }

    private sealed class ResultMap
    {
        private readonly Dictionary<Expression, Result> _mapping;
        private readonly ExpressionReplacer _replacer;

        public ReadOnlyCollection<ParameterExpression> AllDependencies { get; }

        public ResultMap(IEnumerable<Expression?> nodes, in State state)
        {
            var mapping = new Dictionary<Expression, Result>(ReferenceEqualityComparer.Instance);
            foreach (var node in nodes)
            {
                if (node is null || mapping.ContainsKey(node))
                    continue;

                mapping[node] = _instance.Translate(node, state);
            }

            _mapping = mapping;
            _replacer = new(v => _mapping.TryGetValue(v, out var r) ? r.Expression : v)
            {
                DirectOnly = true,
            };
            AllDependencies = [.. mapping.Values.SelectMany(static x => x.Dependencies)];
        }

        public Expression Replace(Expression node)
        {
            return _replacer.Visit(node);
        }

        public Result Get(Expression node)
        {
            return _mapping[node];
        }
    }
}
