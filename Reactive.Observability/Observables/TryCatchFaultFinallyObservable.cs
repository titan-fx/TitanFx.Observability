using System;
using System.Collections.Generic;

namespace Reactive.Observability.Observables;

internal sealed class TryCatchFaultFinallyObservable<T>(
    IObservable<T> body,
    IEnumerable<FilteredExceptionObservable<T>> catches,
    IObservable<T>? fault,
    IObservable<T>? @finally
) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        return new Subscription(body, catches, fault, @finally, observer);
    }

    private sealed class Subscription : IDisposable, IObserver<T>
    {
        private readonly IEnumerable<FilteredExceptionObservable<T>> _catches;
        private readonly IObservable<T>? _fault;
        private readonly IObservable<T>? _finally;
        private readonly IObserver<T> _observer;
        private IDisposable? _subscription;

        public Subscription(
            IObservable<T> body,
            IEnumerable<FilteredExceptionObservable<T>> catches,
            IObservable<T>? fault,
            IObservable<T>? @finally,
            IObserver<T> observer
        )
        {
            _catches = catches;
            _fault = fault;
            _finally = @finally;
            _observer = observer;

            try
            {
                _subscription = body.Subscribe(this);
            }
            catch (Exception ex)
            {
                OnError(ex);
            }
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void OnCompleted()
        {
            if (_finally is not null)
            {
                _subscription?.Dispose();
                _subscription = _finally.Subscribe(_observer);
            }
            else
            {
                _observer.OnCompleted();
            }
        }

        public void OnError(Exception error)
        {
            foreach (var @catch in _catches)
            {
                if (@catch.Filter(error))
                {
                    if (_fault is null)
                    {
                        if (_finally is null)
                            Finally(@catch.Catch(error));
                        else
                            Finally(@catch.Catch(error), _finally);
                    }
                    else if (_finally is null)
                        Finally(@catch.Catch(error), _fault);
                    else
                        Finally(@catch.Catch(error), _fault, _finally);
                }
            }

            if (_fault is null)
            {
                if (_finally is null)
                    Finally(error);
                else
                    Finally(error, _finally);
            }
            else if (_finally is null)
                Finally(error, _fault);
            else
                Finally(error, _fault, _finally);
        }

        private void Finally(IObservable<T> @catch, IObservable<T> fault, IObservable<T> @finally)
        {
            _subscription?.Dispose();
            _subscription = @catch.Subscribe(new Finally2(this, fault, @finally));
        }

        private void Finally(IObservable<T> @catch, IObservable<T> @finally)
        {
            _subscription?.Dispose();
            _subscription = @catch.Subscribe(new Finally1(this, @finally));
        }

        private void Finally(IObservable<T> @catch)
        {
            _subscription?.Dispose();
            _subscription = @catch.Subscribe(_observer);
        }

        private void Finally(Exception error, IObservable<T> fault, IObservable<T> @finally)
        {
            _subscription?.Dispose();
            _subscription = fault.Subscribe(new Rethrow2(this, error, @finally));
        }

        private void Finally(Exception error, IObservable<T> @finally)
        {
            _subscription?.Dispose();
            _subscription = @finally.Subscribe(new Rethrow1(this, error));
        }

        private void Finally(Exception error)
        {
            _observer.OnError(error);
        }

        public void OnNext(T value)
        {
            _observer.OnNext(value);
        }

        private sealed class Rethrow1(Subscription subscription, Exception error) : IObserver<T>
        {
            public void OnCompleted() => subscription.Finally(error);

            public void OnError(Exception error) => subscription.Finally(error);

            public void OnNext(T value) => subscription.OnNext(value);
        }

        private sealed class Rethrow2(
            Subscription subscription,
            Exception error,
            IObservable<T> next
        ) : IObserver<T>
        {
            public void OnCompleted() => subscription.Finally(error, next);

            public void OnError(Exception error) => subscription.Finally(error, next);

            public void OnNext(T value) => subscription.OnNext(value);
        }

        private sealed class Finally1(Subscription subscription, IObservable<T> next) : IObserver<T>
        {
            public void OnCompleted() => subscription.Finally(next);

            public void OnError(Exception error) => subscription.Finally(error, next);

            public void OnNext(T value) => subscription.OnNext(value);
        }

        private sealed class Finally2(
            Subscription subscription,
            IObservable<T> next1,
            IObservable<T> next2
        ) : IObserver<T>
        {
            public void OnCompleted() => subscription.Finally(next1, next2);

            public void OnError(Exception error) => subscription.Finally(error, next1, next2);

            public void OnNext(T value) => subscription.OnNext(value);
        }
    }
}
