using System;
using System.Reflection;
using TitanFx.Observability.Observables;

namespace TitanFx.Observability.Binding;

public interface IReactiveBinderItem
{
    bool IsInstanceSupported<TInstance>(MemberInfo member)
        where TInstance : notnull;
    bool IsStaticSupported(MemberInfo member);
    IObservable<TInstance> WatchInstance<TInstance>(TInstance instance, MemberInfo member)
        where TInstance : notnull;
    IObservable<Nothing> WatchStatic(MemberInfo member);
}
