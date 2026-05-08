using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using TitanFx.Observability.Observables;

namespace TitanFx.Observability.Binding;

public sealed class NotifyCollectionChangedBinderItem
    : ConstrainedReactiveBinderItem<INotifyCollectionChanged>
{
    public override IObservable<TInstance> Watch<TInstance>(TInstance instance, MemberInfo member)
    {
        return new CollectionChangedObservable<TInstance>(instance);
    }

    public override bool IsSupported<TInstance>(MemberInfo member)
    {
        return member.Name is nameof(IEnumerable.GetEnumerator)
            || (member is MethodInfo && member.DeclaringType == typeof(Enumerable));
    }
}
