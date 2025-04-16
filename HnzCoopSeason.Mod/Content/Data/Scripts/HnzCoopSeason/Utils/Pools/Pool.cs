using System;
using System.Collections.Concurrent;

namespace HnzCoopSeason.Utils.Pools
{
    public class Pool<T>
    {
        readonly ConcurrentQueue<T> _queue;
        readonly Func<T> _factory;
        readonly Action<T> _cleanup;

        public Pool(Func<T> factory, Action<T> cleanup)
        {
            _queue = new ConcurrentQueue<T>();
            _factory = factory;
            _cleanup = cleanup;
        }

        public T Get()
        {
            T element;
            if (_queue.TryDequeue(out element))
            {
                return element;
            }

            return _factory();
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