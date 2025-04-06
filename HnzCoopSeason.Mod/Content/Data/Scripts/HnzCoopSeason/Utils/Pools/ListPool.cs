using System;
using System.Collections.Generic;

namespace HnzCoopSeason.Utils.Pools
{
    public sealed class ListPool<T> : Pool<List<T>>
    {
        public static readonly ListPool<T> Instance = new ListPool<T>();

        ListPool() : base(() => new List<T>(), l => l.Clear())
        {
        }
    }
}