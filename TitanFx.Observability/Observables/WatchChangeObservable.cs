using System;

namespace TitanFx.Observability.Observables;

public abstract class WatchChangeObservable<T>(T source) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        var executing = false;
        var result = Subscribe(source, EmitChange);
        if (result is null)
        {
            EmitChange();
            observer.OnCompleted();
            return EmptyDisposable.Instance;
        }

        try
        {
            EmitChange();
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }

        void EmitChange()
        {
            if (executing)
                return;

            executing = true;
            try
            {
                observer.OnNext(source);
            }
            finally
            {
                executing = false;
            }
        }
    }

    protected abstract IDisposable? Subscribe(T source, Action onChange);
}
