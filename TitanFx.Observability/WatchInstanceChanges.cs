using System;

namespace TitanFx.Observability;

public delegate IObservable<TInstance> WatchInstanceChanges<TInstance>(TInstance instance)
    where TInstance : notnull;
