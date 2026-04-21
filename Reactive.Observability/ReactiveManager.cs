using System;
using System.Threading;

namespace Reactive.Observability;

public sealed class ReactiveManager : KeyedDelegateAggregator<string?, Action>
{
    protected override bool IsCatchAll(string? key) => key is null;

    public IDisposable Subscribe(string? memberName, Action handler)
    {
        Add(memberName, handler);
        return new Subscription(this, memberName, handler);
    }

    private sealed class Subscription(ReactiveManager manager, string? memberName, Action handler)
        : IDisposable
    {
        private ReactiveManager _manager = manager;
        private string? _propertyName = memberName;
        private Action _handler = handler;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _manager, value: null!) is { } manager)
            {
                manager.Remove(_propertyName, _handler);
                _propertyName = default;
                _handler = default!;
            }
        }
    }
}
