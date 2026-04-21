using System;
using System.Reactive;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Reactive.Observability.Binding;

public abstract class ConstrainedReactiveBinderItem<TConstraint> : IReactiveBinderItem
{
    IObservable<TInstance> IReactiveBinderItem.WatchInstance<TInstance>(
        TInstance instance,
        MemberInfo member
    )
    {
        if (!typeof(TInstance).IsAssignableTo(typeof(TConstraint)))
            throw new NotSupportedException("Incompatible types");

        return Unsafe.As<ConstrainedReactiveBinderItem<TInstance>>(this).Watch(instance, member);
    }

    bool IReactiveBinderItem.IsInstanceSupported<TInstance>(MemberInfo member)
    {
        if (!typeof(TInstance).IsAssignableTo(typeof(TConstraint)))
            return false;

        return Unsafe
            .As<ConstrainedReactiveBinderItem<TInstance>>(this)
            .IsSupported<TInstance>(member);
    }

    bool IReactiveBinderItem.IsStaticSupported(MemberInfo member) => false;

    IObservable<Unit> IReactiveBinderItem.WatchStatic(MemberInfo member) =>
        throw new NotSupportedException("Static members cannot satisfy an instance constraint");

    public abstract IObservable<TInstance> Watch<TInstance>(TInstance instance, MemberInfo member)
        where TInstance : TConstraint;

    public abstract bool IsSupported<TInstance>(MemberInfo member)
        where TInstance : TConstraint;
}
