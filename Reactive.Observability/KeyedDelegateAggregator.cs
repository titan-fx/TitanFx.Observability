using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Reactive.Observability;

public class KeyedDelegateAggregator<TKey, TDelegate>
    where TDelegate : Delegate
{
    private readonly Impl _impl;

    protected KeyedDelegateAggregator()
    {
        _impl = new Impl(this, EqualityComparer<TKey>.Default);
    }

    protected KeyedDelegateAggregator(IEqualityComparer<TKey> comparer)
    {
        _impl = new Impl(this, comparer);
    }

    public TDelegate? this[TKey key] => _impl.Get(key).Aggregated;

    public void Add(TKey key, TDelegate @delegate)
    {
        _impl.Add(key, @delegate);
    }

    public void Remove(TKey key, TDelegate @delegate)
    {
        _impl.Remove(key, @delegate);
    }

    protected virtual bool IsCatchAll(TKey key)
    {
        return false;
    }

    private struct Context
    {
        private List<TDelegate>? _items;

        public TDelegate? Aggregated
        {
            get => field ??= Aggregate();
            private set;
        }

        private readonly TDelegate? Aggregate()
        {
            return CollectionsMarshal.AsSpan(_items) switch
            {
                [] => null,
                [var x] => x,
                [var x, var y] => DelegateUtil.Combine(x, y),
                var multiple => multiple.BisectAggregate(DelegateUtil.Combine),
            };
        }

        public void Add(TDelegate? item)
        {
            if (item is null)
                return;
            if (_items is null)
                _items = [item];
            else
                _items.Add(item);
            Aggregated = null;
        }

        public void AddAll(Context other)
        {
            if (other._items is null or { Count: 0 })
                return;
            if (_items is null)
                _items = [.. other._items];
            else
                _items.AddRange(other._items);
            Aggregated = null;
        }

        public void Remove(TDelegate? item)
        {
            if (item is not null && _items?.Remove(item) == true)
                Aggregated = null;
        }
    }

    private sealed class Impl(
        KeyedDelegateAggregator<TKey, TDelegate> parent,
        IEqualityComparer<TKey> comparer
    ) : KeyedAggregator<TKey, Context, TDelegate?>(comparer)
    {
        protected override bool IsCatchAll(TKey key)
        {
            return parent.IsCatchAll(key);
        }

        protected override void Add(ref Context context, TDelegate? item)
        {
            context.Add(item);
        }

        protected override void Remove(ref Context context, TDelegate? item)
        {
            context.Remove(item);
        }

        protected override void Merge(ref Context context, Context catchAll)
        {
            context.AddAll(catchAll);
        }
    }
}
