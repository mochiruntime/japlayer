#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Japlayer.Helpers
{
    /// <summary>
    /// Bounded least-recently-used cache for decoded thumbnail images.
    /// </summary>
    public sealed class BoundedLruCache<TKey, TValue> where TKey : notnull
    {
        private readonly int _capacity;
        private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _entries;
        private readonly LinkedList<(TKey Key, TValue Value)> _order = new();
        private readonly object _gate = new();

        public BoundedLruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Cache capacity must be positive.");
            }

            _capacity = capacity;
            _entries = new Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>>(capacity, comparer);
        }

        public int Count
        {
            get
            {
                lock (_gate)
                {
                    return _entries.Count;
                }
            }
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(key, out var node))
                {
                    _order.Remove(node);
                    _order.AddFirst(node);
                    value = node.Value.Value;
                    return true;
                }

                value = default;
                return false;
            }
        }

        public void Set(TKey key, TValue value)
        {
            lock (_gate)
            {
                if (_entries.TryGetValue(key, out var existingNode))
                {
                    _order.Remove(existingNode);
                    existingNode.Value = (key, value);
                    _order.AddFirst(existingNode);
                    return;
                }

                if (_entries.Count >= _capacity && _order.Last is { } oldestNode)
                {
                    _order.RemoveLast();
                    _entries.Remove(oldestNode.Value.Key);
                }

                var newNode = new LinkedListNode<(TKey Key, TValue Value)>((key, value));
                _order.AddFirst(newNode);
                _entries[key] = newNode;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _entries.Clear();
                _order.Clear();
            }
        }
    }
}
