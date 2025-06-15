using System;
using System.Collections.Generic;
using System.Linq;

namespace HnzUtils
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
            return $"{{{string.Join(", ", self.Select(p => $"{p.Key}: {p.Value}"))}}}";
        }

        public static bool TryGetElementAt<T>(this IReadOnlyList<T> self, int index, out T element)
        {
            if (index >= 0 && index < self.Count)
            {
                element = self[index];
                return true;
            }

            element = default(T);
            return false;
        }

        public static T GetElementAtOrDefault<T>(this IReadOnlyList<T> self, int index, T defaultValue)
        {
            T value;
            if (self.TryGetElementAt(index, out value)) return value;
            return defaultValue;
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

        // NOTE this function blows up given duplicate keys.
        // To overwrite duplicate keys, use `ConcatNoDupe()` instead.
        public static Dictionary<K, V> Concat<K, V>(this IReadOnlyDictionary<K, V> self, IReadOnlyDictionary<K, V> second)
        {
            return ((IEnumerable<KeyValuePair<K, V>>)self).Concat(second).ToDictionary(p => p.Key, p => p.Value);
        }

        public static Dictionary<K, V> ConcatNoDupe<K, V>(this IReadOnlyDictionary<K, V> self, IReadOnlyDictionary<K, V> second)
        {
            var concat = self.ToDictionary(p => p.Key, p => p.Value); // duplicate
            foreach (var kvp in second)
            {
                concat[kvp.Key] = kvp.Value;
            }

            return concat;
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