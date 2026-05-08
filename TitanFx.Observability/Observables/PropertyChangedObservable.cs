using System;
using System.ComponentModel;

namespace TitanFx.Observability.Observables;

public sealed class PropertyChangedObservable<T>(T source, string? propertyName)
    : WatchChangeObservable<T>(source)
    where T : INotifyPropertyChanged
{
    protected override IDisposable? Subscribe(T source, Action onChange)
    {
        if (propertyName is null)
            return new Subscription(source, (_, _) => onChange());

        return new Subscription(
            source,
            (_, e) =>
            {
                if (
                    e.PropertyName is null
                    || e.PropertyName.Equals(propertyName, StringComparison.Ordinal)
                )
                {
                    onChange();
                }
            }
        );
    }

    private sealed class Subscription : IDisposable
    {
        private T _source;
        private PropertyChangedEventHandler? _handler;

        public Subscription(T source, PropertyChangedEventHandler handler)
        {
            _source = source;
            _handler = handler;
            _source.PropertyChanged += handler;
        }

        public void Dispose()
        {
            if (_handler is not { } handler)
                return;

            _handler = null;
            var source = _source;
            _source = default!;
            source.PropertyChanged -= handler;
        }
    }
}
