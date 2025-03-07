using System;
using System.Collections.Generic;
using System.Linq;

namespace HnzCoopSeason.Utils
{
    public static class LangUtils
    {
        public static string ToStringSeq<T>(this IEnumerable<T> self)
        {
            return $"[{string.Join(", ", self)}]";
        }

        public static string ToStringDic<K, V>(this IReadOnlyDictionary<K, V> self)
        {
            return $"[{string.Join(", ", self.Select(p => $"{p.Key}: {p.Value}"))}]";
        }

        public static T GetValueOrDefault<T>(this Dictionary<string, object> self, string key, T defaultValue)
        {
            object value;
            if (!self.TryGetValue(key, out value)) return defaultValue;
            if (!(value is T)) return defaultValue;
            return (T)value;
        }

        public static HashSet<T> ToSet<T>(this IEnumerable<T> self)
        {
            return new HashSet<T>(self);
        }
    }
}