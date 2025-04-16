using System;
using System.Collections.Generic;
using System.Linq;

namespace HnzCoopSeason.Utils
{
    public static class LangUtils
    {
        public static void AssertNull(object obj, string message)
        {
            if (obj == null)
            {
                throw new InvalidOperationException(message);
            }
        }
        
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

        public static int ParseIntOrDefault(this string self, int defaultValue)
        {
            int result;
            if (int.TryParse(self, out result))
            {
                return result;
            }

            return defaultValue;
        }

        public static void Increment<K>(this IDictionary<K, int> self, K key, int delta)
        {
            int value;
            self.TryGetValue(key, out value);
            self[key] = value + delta;
        }

        public static void Sort<T, U>(this List<T> self, Func<T, U> f)
        {
            self.Sort((a, b) => Comparer<U>.Default.Compare(f(a), f(b)));
        }
    }
}