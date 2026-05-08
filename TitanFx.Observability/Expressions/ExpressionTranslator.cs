using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using E = System.Linq.Expressions.ExpressionType;

namespace TitanFx.Observability.Expressions;

internal abstract class ExpressionTranslator<TResult, TState>
{
    public Maybe<TResult> TranslateNullable(Expression? node, in TState state)
    {
        if (node is null)
            return default;

        return Translate(node, state);
    }

    public ImmutableArray<TResult> Translate(IReadOnlyCollection<Expression> nodes, in TState state)
    {
        var builder = ImmutableArray.CreateBuilder<TResult>(nodes.Count);
        foreach (var node in nodes)
            builder.Add(Translate(node, state));
        return builder.MoveToImmutable();
    }

    public virtual TResult Translate(Expression node, in TState state)
    {
        return node.NodeType switch
        {
            E.Negate => Negate(node.AsUnary, state),
            E.NegateChecked => NegateX(node.AsUnary, state),
            E.Not => Not(node.AsUnary, state),
            E.IsFalse => IsFalse(node.AsUnary, state),
            E.IsTrue => IsTrue(node.AsUnary, state),
            E.OnesComplement => Invert(node.AsUnary, state),
            E.ArrayLength => ArrayLength(node.AsUnary, state),
            E.Convert => Convert(node.AsUnary, state),
            E.ConvertChecked => ConvertX(node.AsUnary, state),
            E.Throw => Throw(node.AsUnary, state),
            E.TypeAs => As(node.AsUnary, state),
            E.Quote => Quote(node.AsUnary, state),
            E.UnaryPlus => Plus(node.AsUnary, state),
            E.Unbox => Unbox(node.AsUnary, state),
            E.Increment => Inc(node.AsUnary, state),
            E.Decrement => Dec(node.AsUnary, state),
            E.PreIncrementAssign => IncAssign(node.AsUnary, state),
            E.PostIncrementAssign => AssignInc(node.AsUnary, state),
            E.PreDecrementAssign => DecAssign(node.AsUnary, state),
            E.PostDecrementAssign => AssignDec(node.AsUnary, state),
            E.Add => Add(node.AsBinary, state),
            E.AddChecked => AddX(node.AsBinary, state),
            E.Subtract => Sub(node.AsBinary, state),
            E.SubtractChecked => SubX(node.AsBinary, state),
            E.Multiply => Mul(node.AsBinary, state),
            E.MultiplyChecked => MulX(node.AsBinary, state),
            E.Divide => Div(node.AsBinary, state),
            E.Modulo => Mod(node.AsBinary, state),
            E.Power => Pow(node.AsBinary, state),
            E.And => And(node.AsBinary, state),
            E.AndAlso => AndAlso(node.AsBinary, state),
            E.Or => Or(node.AsBinary, state),
            E.OrElse => OrElse(node.AsBinary, state),
            E.LessThan => LT(node.AsBinary, state),
            E.LessThanOrEqual => LE(node.AsBinary, state),
            E.GreaterThan => GT(node.AsBinary, state),
            E.GreaterThanOrEqual => GE(node.AsBinary, state),
            E.Equal => Eq(node.AsBinary, state),
            E.NotEqual => NE(node.AsBinary, state),
            E.ExclusiveOr => XOr(node.AsBinary, state),
            E.Coalesce => Coalesce(node.AsBinary, state),
            E.ArrayIndex => ArrayIndex(node.AsBinary, state),
            E.RightShift => ShiftR(node.AsBinary, state),
            E.LeftShift => ShiftL(node.AsBinary, state),
            E.Assign => Assign(node.AsBinary, state),
            E.AddAssign => AddAssign(node.AsBinary, state),
            E.AndAssign => AndAssign(node.AsBinary, state),
            E.DivideAssign => DivAssign(node.AsBinary, state),
            E.ExclusiveOrAssign => XOrAssign(node.AsBinary, state),
            E.LeftShiftAssign => ShiftLAssign(node.AsBinary, state),
            E.ModuloAssign => ModAssign(node.AsBinary, state),
            E.MultiplyAssign => MulAssign(node.AsBinary, state),
            E.OrAssign => OrAssign(node.AsBinary, state),
            E.PowerAssign => PowerAssign(node.AsBinary, state),
            E.RightShiftAssign => ShiftRAssign(node.AsBinary, state),
            E.SubtractAssign => SubAssign(node.AsBinary, state),
            E.AddAssignChecked => AddXAssign(node.AsBinary, state),
            E.SubtractAssignChecked => SubXAssign(node.AsBinary, state),
            E.MultiplyAssignChecked => MulXAssign(node.AsBinary, state),
            E.TypeIs => TypeIs(node.AsTypeBinary, state),
            E.TypeEqual => TypeEq(node.AsTypeBinary, state),
            E.NewArrayInit => NewArray(node.AsNewArray, state),
            E.NewArrayBounds => NewArraySize(node.AsNewArray, state),
            E.Call => Call(node.AsMethodCall, state),
            E.Conditional => Ternary(node.AsConditional, state),
            E.Constant => Constant(node.AsConstant, state),
            E.Invoke => Invoke(node.AsInvocation, state),
            E.Lambda => Lambda(node.AsLambda, state),
            E.ListInit => ListInit(node.AsListInit, state),
            E.MemberAccess => Member(node.AsMember, state),
            E.MemberInit => MemberInit(node.AsMemberInit, state),
            E.New => ProcessNew(node.AsNew, state),
            E.Parameter => Parameter(node.AsParameter, state),
            E.Block => Block(node.AsBlock, state),
            E.DebugInfo => DebugInfo(node.AsDebugInfo, state),
            E.Dynamic => Dynamic(node.AsDynamic, state),
            E.Default => Default(node.AsDefault, state),
            E.Extension => Extension(node, state),
            E.Goto => ProcessGoto(node.AsGoto, state),
            E.Index => ProcessIndex(node.AsIndex, state),
            E.Label => Label(node.AsLabel, state),
            E.RuntimeVariables => RuntimeVariables(node.AsRuntimeVariables, state),
            E.Loop => Loop(node.AsLoop, state),
            E.Switch => Switch(node.AsSwitch, state),
            E.Try => Try(node.AsTry, state),
            _ => throw new UnreachableException(),
        };
    }

    private TResult ProcessIndex(IndexExpression node, in TState state) =>
        node.Indexer is null ? Index(node, state) : Index(node, node.Indexer, state);

    private TResult ProcessNew(NewExpression node, in TState state) =>
        node.Constructor is null
            ? NewParameterless(node, state)
            : New(node, node.Constructor, state);

    private TResult ProcessGoto(GotoExpression node, in TState state) =>
        node.Kind switch
        {
            GotoExpressionKind.Goto => Goto(node, state),
            GotoExpressionKind.Return => Return(node, state),
            GotoExpressionKind.Break => Break(node, state),
            GotoExpressionKind.Continue => Continue(node, state),
            _ => throw new UnreachableException(),
        };

    protected virtual TResult Try(TryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Switch(SwitchExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Loop(LoopExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult RuntimeVariables(RuntimeVariablesExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Label(LabelExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Index(IndexExpression node, PropertyInfo indexer, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Index(IndexExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Goto(GotoExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Break(GotoExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Continue(GotoExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Return(GotoExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Extension(Expression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Default(DefaultExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Dynamic(DynamicExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult DebugInfo(DebugInfoExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Block(BlockExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Parameter(ParameterExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult NewParameterless(NewExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult New(
        NewExpression node,
        ConstructorInfo constructor,
        in TState state
    ) => throw new NotSupportedException();

    protected virtual TResult MemberInit(MemberInitExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Member(MemberExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ListInit(ListInitExpression node, in TState state) =>
        throw new NotSupportedException();

    private abstract class LambdaRouter
    {
        public abstract TResult Route(
            ExpressionTranslator<TResult, TState> processor,
            LambdaExpression node,
            in TState state
        );
    }

    private sealed class LambdaRouter<T> : LambdaRouter
        where T : Delegate
    {
        public override TResult Route(
            ExpressionTranslator<TResult, TState> processor,
            LambdaExpression node,
            in TState state
        )
        {
            return processor.Lambda(Unsafe.As<Expression<T>>(node), state);
        }
    }

    private static readonly ConcurrentDictionary<Type, LambdaRouter> _routers = [];

    protected virtual TResult Lambda(LambdaExpression node, in TState state)
    {
        var router = _routers.GetOrAdd(
            node.Type,
            static t =>
                (LambdaRouter)
                    Activator.CreateInstance(
                        typeof(LambdaRouter<>).MakeGenericType(typeof(TResult), typeof(TState), t)
                    )!
        );
        return router.Route(this, node, state);
    }

    protected virtual TResult Lambda<T>(Expression<T> node, in TState state)
        where T : Delegate => throw new NotSupportedException();

    protected virtual TResult Invoke(InvocationExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Constant(ConstantExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Ternary(ConditionalExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Call(MethodCallExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult NewArraySize(NewArrayExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult NewArray(NewArrayExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult TypeEq(TypeBinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult TypeIs(TypeBinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult MulXAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult SubXAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult AddXAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult SubAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ShiftRAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult PowerAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult OrAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult MulAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ModAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ShiftLAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult XOrAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult DivAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult AndAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult AddAssign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Assign(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ShiftL(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ShiftR(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ArrayIndex(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Coalesce(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult XOr(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult NE(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Eq(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult GE(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult GT(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult LE(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult LT(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult OrElse(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Or(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult AndAlso(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult And(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Pow(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Mod(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Div(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult MulX(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Mul(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult SubX(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Sub(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult AddX(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Add(BinaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult AssignDec(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult DecAssign(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult AssignInc(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult IncAssign(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Dec(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Inc(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Unbox(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Plus(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Quote(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult As(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Throw(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ConvertX(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Convert(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult ArrayLength(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Invert(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult IsTrue(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult IsFalse(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Not(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult NegateX(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();

    protected virtual TResult Negate(UnaryExpression node, in TState state) =>
        throw new NotSupportedException();
}

file static class Ext
{
    extension(Expression node)
    {
        public BinaryExpression AsBinary => Unsafe.As<BinaryExpression>(node);
        public UnaryExpression AsUnary => Unsafe.As<UnaryExpression>(node);
        public TypeBinaryExpression AsTypeBinary => Unsafe.As<TypeBinaryExpression>(node);
        public NewArrayExpression AsNewArray => Unsafe.As<NewArrayExpression>(node);
        public MethodCallExpression AsMethodCall => Unsafe.As<MethodCallExpression>(node);
        public ConditionalExpression AsConditional => Unsafe.As<ConditionalExpression>(node);
        public ConstantExpression AsConstant => Unsafe.As<ConstantExpression>(node);
        public InvocationExpression AsInvocation => Unsafe.As<InvocationExpression>(node);
        public LambdaExpression AsLambda => Unsafe.As<LambdaExpression>(node);
        public ListInitExpression AsListInit => Unsafe.As<ListInitExpression>(node);
        public MemberExpression AsMember => Unsafe.As<MemberExpression>(node);
        public MemberInitExpression AsMemberInit => Unsafe.As<MemberInitExpression>(node);
        public NewExpression AsNew => Unsafe.As<NewExpression>(node);
        public ParameterExpression AsParameter => Unsafe.As<ParameterExpression>(node);
        public BlockExpression AsBlock => Unsafe.As<BlockExpression>(node);
        public DebugInfoExpression AsDebugInfo => Unsafe.As<DebugInfoExpression>(node);
        public DynamicExpression AsDynamic => Unsafe.As<DynamicExpression>(node);
        public DefaultExpression AsDefault => Unsafe.As<DefaultExpression>(node);
        public GotoExpression AsGoto => Unsafe.As<GotoExpression>(node);
        public IndexExpression AsIndex => Unsafe.As<IndexExpression>(node);
        public LabelExpression AsLabel => Unsafe.As<LabelExpression>(node);
        public RuntimeVariablesExpression AsRuntimeVariables =>
            Unsafe.As<RuntimeVariablesExpression>(node);
        public LoopExpression AsLoop => Unsafe.As<LoopExpression>(node);
        public SwitchExpression AsSwitch => Unsafe.As<SwitchExpression>(node);
        public TryExpression AsTry => Unsafe.As<TryExpression>(node);
    }
}
