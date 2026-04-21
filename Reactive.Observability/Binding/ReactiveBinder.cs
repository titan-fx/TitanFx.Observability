using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Reactive.Observability.Binding;

public sealed class ReactiveBinder(params IEnumerable<IReactiveBinderItem> items) : IReactiveBinder
{
    private readonly IReactiveBinderItem[] _items = [.. items];

    public WatchExtensionChanges<TInstance>? WatchExtension<TInstance>(MethodInfo extensionMethod)
        where TInstance : notnull
    {
        var result = WatchInstance<TInstance>(extensionMethod);
        if (result is null)
            return null;

        return result.Method.CreateDelegate<WatchExtensionChanges<TInstance>>(result.Target);
    }

    public WatchInstanceChanges<TInstance>? WatchInstance<TInstance>(MemberInfo instanceMember)
        where TInstance : notnull
    {
        if (_items.Length == 0)
            return null;

        var binder = _items[0];
        if (binder.IsInstanceSupported<TInstance>(instanceMember))
            return src => binder.WatchInstance(src, instanceMember);

        if (CanBeExtended(typeof(TInstance)))
        {
            return src =>
            {
                var proxy = BinderProxy.For(src.GetType());
                foreach (var binder in _items)
                    if (proxy.IsSupported(binder, instanceMember))
                        return proxy.Create(binder, src, instanceMember);

                return Observable.Return(src);
            };
        }

        for (var i = 1; i < _items.Length; i++)
        {
            binder = _items[i];
            if (binder.IsInstanceSupported<TInstance>(instanceMember))
                return src => binder.WatchInstance(src, instanceMember);
        }

        return null;
    }

    public WatchStaticChanges? WatchStatic(MemberInfo staticMember)
    {
        foreach (var binder in _items)
            if (binder.IsStaticSupported(staticMember))
                return () => binder.WatchStatic(staticMember);

        return null;
    }

    private static bool CanBeExtended(Type type)
    {
        return type
            is {
                IsSealed: false,
                IsArray: false,
                IsValueType: false,
                IsEnum: false,
                IsPrimitive: false,
            };
    }

    private abstract class BinderProxy
    {
        private static readonly ConcurrentDictionary<Type, BinderProxy> _cache = [];

        public static BinderProxy For(Type type)
        {
            return _cache.GetOrAdd(
                type,
                static t =>
                    Unsafe.As<BinderProxy>(
                        Activator.CreateInstance(typeof(Impl<>).MakeGenericType(t))!
                    )
            );
        }

        private BinderProxy() { }

        public abstract bool IsSupported(IReactiveBinderItem item, MemberInfo member);
        public abstract IObservable<TInstance> Create<TInstance>(
            IReactiveBinderItem item,
            TInstance instance,
            MemberInfo member
        );

        private sealed class Impl<T> : BinderProxy
            where T : notnull
        {
            public override bool IsSupported(IReactiveBinderItem item, MemberInfo member)
            {
                return item.IsInstanceSupported<T>(member);
            }

            public override IObservable<TInstance> Create<TInstance>(
                IReactiveBinderItem item,
                TInstance instance,
                MemberInfo member
            )
            {
                return Unsafe.As<IObservable<TInstance>>(
                    item.WatchInstance<T>(Unsafe.As<TInstance, T>(ref instance), member)
                );
            }
        }
    }
}
