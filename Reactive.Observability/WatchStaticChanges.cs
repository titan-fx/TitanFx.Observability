using System;
using System.Reactive;

namespace Reactive.Observability;

public delegate IObservable<Unit> WatchStaticChanges();
