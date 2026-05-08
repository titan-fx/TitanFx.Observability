using System;

namespace TitanFx.Observability.Observables;

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
            return EmptyDisposable.Instance;
        }
        observer.OnCompleted();
        return EmptyDisposable.Instance;
    }
}
