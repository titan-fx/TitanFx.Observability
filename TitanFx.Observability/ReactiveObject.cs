using System;
using System.Buffers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TitanFx.Observability;

public abstract class ReactiveObject : IReactive, INotifyPropertyChanged
{
    private readonly ReactiveManager _manager = new();

    public event PropertyChangedEventHandler? PropertyChanged;

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

    protected virtual bool IsProperty(string? memberName)
    {
        return memberName is null || Properties.For(GetType()).Contains(memberName);
    }

    protected virtual void OnMemberChanged(params ReadOnlySpan<string?> memberNames)
    {
        foreach (var propertyName in memberNames)
        {
            _manager[propertyName]?.Invoke();
            if (PropertyChanged is not null && IsProperty(propertyName))
                PropertyChanged?.Invoke(this, Properties.ChangedArg(propertyName));
        }
    }

    public IDisposable Watch(string? propertyName, Action handler)
    {
        return _manager.Subscribe(propertyName, handler);
    }
}

file sealed class Properties
{
    private static readonly Dictionary<Type, SearchValues<string>> _cache = [];

    public static SearchValues<string> For(Type type)
    {
        if (_cache.TryGetValue(type, out var filter))
            return filter;

        var properties = type.GetProperties(
            BindingFlags.Static
                | BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
        );
        filter = SearchValues.Create(
            properties
                .SelectMany(
                    static IEnumerable<string> (x) =>
                        x switch
                        {
                            { GetMethod: null } => [],
                            { GetMethod: var m } when m.GetParameters().Length > 0 =>
                            [
                                x.Name,
                                $"{x.Name}[]",
                            ],
                            _ => [x.Name],
                        }
                )
                .ToArray(),
            StringComparison.Ordinal
        );
        _cache.TryAdd(type, filter);
        return filter;
    }

    private static readonly PropertyChangedEventArgs _eventArgNull = new(propertyName: null);
    private static readonly Dictionary<string, PropertyChangedEventArgs> _eventArgs = [];

    public static PropertyChangedEventArgs ChangedArg(string? name)
    {
        if (name is null)
            return _eventArgNull;

        if (_eventArgs.TryGetValue(name, out var result))
            return result;

        result = new(name);
        _eventArgs.TryAdd(name, result);
        return result;
    }
}
