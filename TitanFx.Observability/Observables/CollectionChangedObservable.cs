using System;
using System.Collections.Specialized;

namespace TitanFx.Observability.Observables;

public sealed class CollectionChangedObservable<T>(T source) : WatchChangeObservable<T>(source)
    where T : INotifyCollectionChanged
{
    protected override IDisposable? Subscribe(T source, Action onChange)
    {
        return new Subscription(source, (_, _) => onChange());
    }

    private sealed class Subscription : IDisposable
    {
        private T _source;
        private NotifyCollectionChangedEventHandler? _handler;

        public Subscription(T source, NotifyCollectionChangedEventHandler handler)
        {
            _source = source;
            _handler = handler;
            source.CollectionChanged += handler;
        }

        public void Dispose()
        {
            if (_handler is not { } handler)
                return;

            _handler = null;
            var source = _source;
            _source = default!;
            source.CollectionChanged -= handler;
        }
    }
}
