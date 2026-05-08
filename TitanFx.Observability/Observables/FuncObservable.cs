using System;

namespace TitanFx.Observability.Observables;

internal sealed class FuncObservable<T>(Func<T> onSubscribe) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        T result;
        try
        {
            result = onSubscribe();
        }
        catch (Exception ex)
        {
            observer.OnError(ex);
            return EmptyDisposable.Instance;
        }
        observer.OnNext(result);
        observer.OnCompleted();
        return EmptyDisposable.Instance;
    }
}
