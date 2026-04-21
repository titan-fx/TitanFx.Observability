using System;
using System.Reactive.Disposables;

namespace Reactive.Observability.Observables;

internal sealed class ActionObservable(Action onSubscribe) : IObservable<Never>
{
    public IDisposable Subscribe(IObserver<Never> observer)
    {
        try
        {
            onSubscribe();
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
            return Disposable.Empty;
        }
        observer.OnCompleted();
        return Disposable.Empty;
    }
}
