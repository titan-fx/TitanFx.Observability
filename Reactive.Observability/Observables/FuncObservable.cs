using System;
using System.Reactive.Disposables;

namespace Reactive.Observability.Observables;

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
            return Disposable.Empty;
        }
        observer.OnNext(result);
        observer.OnCompleted();
        return Disposable.Empty;
    }
}
