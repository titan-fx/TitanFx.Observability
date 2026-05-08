using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace TitanFx.Observability;

public abstract class ReactiveObject : IReactive
{
    private readonly ReactiveManager _manager = new();

    protected virtual void Set<T>(
        ref T field,
        T value,
        [CallerMemberName] string? memberName = null
    )
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnMemberChanged(memberName);
        }
    }

    protected virtual void Set<T>(ref T field, T value, params ReadOnlySpan<string?> memberNames)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            OnMemberChanged(memberNames);
        }
    }

    protected virtual void Set<T>(
        ref T field,
        T value,
        IEqualityComparer<T> comparer,
        [CallerMemberName] string? memberName = null
    )
    {
        if (!comparer.Equals(field, value))
        {
            field = value;
            OnMemberChanged(memberName);
        }
    }

    protected virtual void Set<T>(
        ref T field,
        T value,
        IEqualityComparer<T> comparer,
        params ReadOnlySpan<string?> memberNames
    )
    {
        if (!comparer.Equals(field, value))
        {
            field = value;
            OnMemberChanged(memberNames);
        }
    }

    protected virtual void OnMemberChanged(params ReadOnlySpan<string?> memberNames)
    {
        foreach (var propertyName in memberNames)
            _manager[propertyName]?.Invoke();
    }

    public IDisposable Watch(string? propertyName, Action handler)
    {
        return _manager.Subscribe(propertyName, handler);
    }
}
