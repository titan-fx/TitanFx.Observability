using System;
using TitanFx.Observability.Observables;

namespace TitanFx.Observability;

public delegate IObservable<Nothing> WatchStaticChanges();
