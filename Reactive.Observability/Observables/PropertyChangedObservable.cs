using System;
using System.ComponentModel;
using System.Reactive.Disposables;

namespace Reactive.Observability.Observables;

public sealed class PropertyChangedObservable<T>(T source, string? propertyName)
    : WatchChangeObservable<T>(source)
    where T : INotifyPropertyChanged
{
    protected override IDisposable? Subscribe(T source, Action onChange)
    {
        if (propertyName is null)
        {
            source.PropertyChanged += OnAnyPropertyChanged;
            return Disposable.Create(() => source.PropertyChanged -= OnAnyPropertyChanged);
            void OnAnyPropertyChanged(object? sender, PropertyChangedEventArgs e) => onChange();
        }

        source.PropertyChanged += OnPropertyChanged;
        return Disposable.Create(() => source.PropertyChanged -= OnPropertyChanged);
        void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (
                e.PropertyName is null
                || e.PropertyName.Equals(propertyName, StringComparison.Ordinal)
            )
            {
                onChange();
            }
        }
    }
}
