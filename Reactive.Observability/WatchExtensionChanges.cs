using System;

namespace Reactive.Observability;

public delegate IObservable<TInstance> WatchExtensionChanges<TInstance>(TInstance instance)
    where TInstance : notnull;
