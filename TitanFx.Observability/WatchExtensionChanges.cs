using System;

namespace TitanFx.Observability;

public delegate IObservable<TInstance> WatchExtensionChanges<TInstance>(TInstance instance)
    where TInstance : notnull;
