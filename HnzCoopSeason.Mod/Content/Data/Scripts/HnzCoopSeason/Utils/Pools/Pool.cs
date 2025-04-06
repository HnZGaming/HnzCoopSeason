using System;
using System.Collections.Generic;

namespace HnzCoopSeason.Utils.Pools
{
    public class Pool<T>
    {
        readonly Queue<T> _queue;
        readonly Func<T> _factory;
        readonly Action<T> _cleanup;

        public Pool(Func<T> factory, Action<T> cleanup)
        {
            _queue = new Queue<T>();
            _factory = factory;
            _cleanup = cleanup;
        }

        public T Get()
        {
            return _queue.Count == 0
                ? _factory()
                : _queue.Dequeue();
        }

        public void Release(T item)
        {
            _cleanup(item);
            _queue.Enqueue(item);
        }

        public IDisposable GetUntilDispose(out T item)
        {
            item = Get();
            return new UntilDispose(this, item);
        }

        public void Clear()
        {
            _queue.Clear();
        }

        struct UntilDispose : IDisposable
        {
            readonly Pool<T> _pool;
            readonly T _item;

            public UntilDispose(Pool<T> pool, T item)
            {
                _pool = pool;
                _item = item;
            }

            public void Dispose()
            {
                _pool.Release(_item);
            }
        }
    }
}