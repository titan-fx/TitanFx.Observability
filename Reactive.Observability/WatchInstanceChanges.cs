using System;

namespace Reactive.Observability;

public delegate IObservable<TInstance> WatchInstanceChanges<TInstance>(TInstance instance)
    where TInstance : notnull;
