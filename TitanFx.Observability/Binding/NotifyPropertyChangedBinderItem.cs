using System;
using System.ComponentModel;
using System.Reflection;
using TitanFx.Observability.Observables;

namespace TitanFx.Observability.Binding;

public sealed class NotifyPropertyChangedBinderItem
    : ConstrainedReactiveBinderItem<INotifyPropertyChanged>
{
    public override IObservable<TInstance> Watch<TInstance>(TInstance instance, MemberInfo member)
    {
        return new PropertyChangedObservable<TInstance>(instance, member.Name);
    }

    public override bool IsSupported<TInstance>(MemberInfo member)
    {
        return member is PropertyInfo;
    }
}
