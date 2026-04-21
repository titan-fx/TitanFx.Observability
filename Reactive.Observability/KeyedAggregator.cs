using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Reactive.Observability;

public abstract class KeyedAggregator<TKey, TAggregate, TItem>(IEqualityComparer<TKey> comparer)
    where TItem : allows ref struct
{
    private TAggregate? _all;
    private TAggregate? _catchAll;
    private readonly Dictionary<Key, Entry> _keyed = [];

    protected abstract bool IsCatchAll(TKey key);
    protected abstract void Add(ref TAggregate? context, TItem item);
    protected abstract void Merge(ref TAggregate? context, TAggregate? catchAll);
    protected abstract void Remove(ref TAggregate? context, TItem item);

    public TAggregate? Get(TKey key)
    {
        if (IsCatchAll(key))
        {
            return _all;
        }

        if (_keyed.TryGetValue(new(key, comparer), out var entry))
        {
            return entry.context;
        }

        return _catchAll;
    }

    private ref Entry GetOrNull(Key key)
    {
        return ref CollectionsMarshal.GetValueRefOrNullRef(_keyed, key);
    }

    private ref Entry GetOrAdd(Key key, out bool exists)
    {
        return ref CollectionsMarshal.GetValueRefOrAddDefault(_keyed, key, out exists);
    }

    public void Add(TKey key, TItem item)
    {
        Add(ref _all, item);
        if (IsCatchAll(key))
        {
            Add(ref _catchAll, item);
            foreach (var k in _keyed.Keys)
            {
                Add(ref GetOrNull(k).context, item);
            }
        }
        else
        {
            var k = new Key(key, comparer);
            ref var entry = ref GetOrAdd(k, out var exists);
            if (!exists)
                Merge(ref entry.context, _catchAll);
            Add(ref entry.context, item);
            entry.nonCatchAllCount++;
        }
    }

    public void Remove(TKey key, TItem item)
    {
        Remove(ref _all, item);
        if (IsCatchAll(key))
        {
            Remove(ref _catchAll, item);
            foreach (var k in _keyed.Keys)
            {
                Remove(ref GetOrNull(k).context, item);
            }
        }
        else
        {
            var k = new Key(key, comparer);
            ref var entry = ref GetOrNull(k);
            if (!Unsafe.IsNullRef(ref entry))
            {
                if (entry.nonCatchAllCount == 1)
                {
                    _ = _keyed.Remove(k);
                }
                else
                {
                    Remove(ref entry.context, item);
                    entry.nonCatchAllCount--;
                }
            }
        }
    }

    private readonly struct Key(TKey key, IEqualityComparer<TKey> comparer) : IEquatable<Key>
    {
        private readonly TKey _key = key;

        public override int GetHashCode() => _key is null ? 0 : comparer.GetHashCode(_key);

        public override bool Equals([NotNullWhen(true)] object? obj) => obj is Key k && Equals(k);

        public bool Equals(Key other)
        {
            return comparer.Equals(_key, other._key);
        }
    }

    private struct Entry
    {
        public TAggregate? context;
        public int nonCatchAllCount;
    }
}
