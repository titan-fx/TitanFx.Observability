using System;
using System.Reflection;
using TitanFx.Observability.Observables;

namespace TitanFx.Observability.Binding;

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
