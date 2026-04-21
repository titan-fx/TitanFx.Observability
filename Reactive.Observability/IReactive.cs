using System;

namespace Reactive.Observability;

public interface IReactive
{
    IDisposable Watch(string? memberName, Action onChange);
}
