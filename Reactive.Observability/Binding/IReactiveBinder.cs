using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Reactive.Observability.Binding;

public interface IReactiveBinder
{
    WatchInstanceChanges<TInstance>? WatchInstance<TInstance>(MemberInfo instanceMember)
        where TInstance : notnull;

    WatchExtensionChanges<TInstance>? WatchExtension<TInstance>(MethodInfo extensionMethod)
        where TInstance : notnull;

    WatchStaticChanges? WatchStatic(MemberInfo staticMember);

    internal Delegate? Watch(Type? instanceType, MemberInfo member)
    {
        if (instanceType is null)
            return WatchStatic(member);

        if (member is MethodInfo { IsStatic: true } method)
            return BinderRouter.WatchExtension(this, instanceType, method);

        return BinderRouter.WatchInstance(this, instanceType, member);
    }
}

file abstract class BinderRouter
{
    private static readonly ConcurrentDictionary<Type, BinderRouter> _routers = [];

    public static Delegate? WatchInstance(
        IReactiveBinder binder,
        Type instanceType,
        MemberInfo member
    )
    {
        return For(instanceType).WatchInstance(binder, member);
    }

    public static Delegate? WatchExtension(
        IReactiveBinder binder,
        Type instanceType,
        MethodInfo method
    )
    {
        return For(instanceType).WatchExtension(binder, method);
    }

    private static BinderRouter For(Type instanceType)
    {
        return _routers.GetOrAdd(
            instanceType,
            static t =>
                Unsafe.As<BinderRouter>(
                    Activator.CreateInstance(typeof(Impl<>).MakeGenericType(t))!
                )
        );
    }

    public abstract Delegate? WatchInstance(IReactiveBinder binder, MemberInfo member);
    public abstract Delegate? WatchExtension(IReactiveBinder binder, MethodInfo method);

    private BinderRouter() { }

    private sealed class Impl<TInstance> : BinderRouter
        where TInstance : notnull
    {
        public override Delegate? WatchInstance(IReactiveBinder binder, MemberInfo member)
        {
            return binder.WatchInstance<TInstance>(member);
        }

        public override Delegate? WatchExtension(IReactiveBinder binder, MethodInfo method)
        {
            return binder.WatchExtension<TInstance>(method);
        }
    }
}
