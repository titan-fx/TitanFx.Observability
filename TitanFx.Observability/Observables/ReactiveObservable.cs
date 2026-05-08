using System;

namespace TitanFx.Observability.Observables;

public sealed class ReactiveObservable<T>(T source, string? memberName)
    : WatchChangeObservable<T>(source)
    where T : IReactive
{
    protected override IDisposable? Subscribe(T source, Action onChange)
    {
        return source.Watch(memberName, onChange);
    }
}
