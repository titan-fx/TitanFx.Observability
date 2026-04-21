using System;
using System.Collections.Specialized;
using System.Reactive.Disposables;

namespace Reactive.Observability.Observables;

public sealed class CollectionChangedObservable<T>(T source) : WatchChangeObservable<T>(source)
    where T : INotifyCollectionChanged
{
    protected override IDisposable? Subscribe(T source, Action onChange)
    {
        source.CollectionChanged += OnCollectionChanged;
        return Disposable.Create(() => source.CollectionChanged -= OnCollectionChanged);
        void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => onChange();
    }
}
