using System;
using System.Reactive;
using System.Reflection;

namespace Reactive.Observability.Binding;

public interface IReactiveBinderItem
{
    bool IsInstanceSupported<TInstance>(MemberInfo member)
        where TInstance : notnull;
    bool IsStaticSupported(MemberInfo member);
    IObservable<TInstance> WatchInstance<TInstance>(TInstance instance, MemberInfo member)
        where TInstance : notnull;
    IObservable<Unit> WatchStatic(MemberInfo member);
}
