using System;

namespace TitanFx.Observability.Observables;

internal sealed class ConditionalObservable<TIn, TOut>(
    IObservable<TIn> source,
    Func<TIn, bool> condition,
    Func<TIn, IObservable<TOut>> ifTrue,
    Func<TIn, IObservable<TOut>> ifFalse
) : IObservable<TOut>
{
    public IDisposable Subscribe(IObserver<TOut> observer)
    {
        return new Subscription(source, condition, ifTrue, ifFalse, observer);
    }

    private sealed class Subscription : IObserver<TIn>, IDisposable
    {
        private readonly IDisposable _subscription;
        private readonly Func<TIn, bool> _condition;
        private readonly Func<TIn, IObservable<TOut>> _ifTrue;
        private readonly Func<TIn, IObservable<TOut>> _ifFalse;
        private readonly IObserver<TOut> _observer;
        private readonly Inner _inner;
        private IDisposable? _innerSubscription;
        private bool _completed;

        public Subscription(
            IObservable<TIn> source,
            Func<TIn, bool> condition,
            Func<TIn, IObservable<TOut>> ifTrue,
            Func<TIn, IObservable<TOut>> ifFalse,
            IObserver<TOut> observer
        )
        {
            _condition = condition;
            _ifTrue = ifTrue;
            _ifFalse = ifFalse;
            _observer = observer;
            _inner = new(this);
            _subscription = source.Subscribe(this);
        }

        public void OnCompleted()
        {
            _completed = true;
            if (_innerSubscription is null)
                _observer.OnCompleted();
        }

        public void OnError(Exception error)
        {
            _innerSubscription?.Dispose();
            _innerSubscription = null;
            _observer.OnError(error);
        }

        public void OnNext(TIn value)
        {
            _innerSubscription?.Dispose();
            var inner = _condition(value) ? _ifTrue(value) : _ifFalse(value);
            _innerSubscription = inner.Subscribe(_inner);
        }

        public void Dispose()
        {
            using (_innerSubscription)
            using (_subscription) { }
        }

        private sealed class Inner(Subscription parent) : IObserver<TOut>
        {
            public void OnCompleted()
            {
                using (parent._innerSubscription)
                {
                    parent._innerSubscription = null;
                    if (parent._completed)
                        parent._observer.OnCompleted();
                }
            }

            public void OnError(Exception error)
            {
                parent.OnError(error);
            }

            public void OnNext(TOut value)
            {
                parent._observer.OnNext(value);
            }
        }
    }
}
