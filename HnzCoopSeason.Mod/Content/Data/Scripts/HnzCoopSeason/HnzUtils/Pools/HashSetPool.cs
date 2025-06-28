using System.Collections.Generic;

namespace HnzUtils.Pools
{
    public sealed class HashSetPool<T> : Pool<HashSet<T>>
    {
        public static readonly HashSetPool<T> Instance = new HashSetPool<T>();

        public HashSetPool() : base(() => new HashSet<T>(), s => s.Clear())
        {
        }
    }
}