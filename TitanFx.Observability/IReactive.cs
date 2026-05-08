using System;

namespace TitanFx.Observability;

public interface IReactive
{
    IDisposable Watch(string? memberName, Action onChange);
}
