using System;
using System.Collections.Concurrent;
using System.Reactive.Disposables;

namespace Reactive.Observability.Observables;

internal class DefaultObservable<T> : IObservable<T?>
{
    public IDisposable Subscribe(IObserver<T?> observer)
    {
        observer.OnNext(default);
        observer.OnCompleted();
        return Disposable.Empty;
    }
}

internal static class DefaultObservable
{
    private static readonly ConcurrentDictionary<Type, object> _cache = [];

    public static object Get(Type type)
    {
        return _cache.GetOrAdd(
            type,
            t => Activator.CreateInstance(typeof(DefaultObservable<>).MakeGenericType(t))!
        );
    }
}
