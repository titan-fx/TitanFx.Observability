using System;

namespace Reactive.Observability.Observables;

internal sealed record FilteredExceptionObservable<T>(
    Func<Exception, bool> Filter,
    Func<Exception, IObservable<T>> Catch
);
