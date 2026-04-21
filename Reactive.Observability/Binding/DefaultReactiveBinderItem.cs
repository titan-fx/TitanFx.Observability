using System;
using System.Reflection;
using Reactive.Observability.Observables;

namespace Reactive.Observability.Binding;

public sealed class DefaultReactiveBinderItem : ConstrainedReactiveBinderItem<IReactive>
{
    public override IObservable<TInstance> Watch<TInstance>(TInstance instance, MemberInfo member)
    {
        return new ReactiveObservable<TInstance>(instance, member.Name);
    }

    public override bool IsSupported<TInstance>(MemberInfo member)
    {
        return true;
    }
}
