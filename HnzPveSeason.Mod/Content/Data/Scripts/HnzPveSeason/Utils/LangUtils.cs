using System;
using System.Collections.Generic;
using System.Linq;

namespace HnzPveSeason.Utils
{
    public static class LangUtils
    {
        public static string ToStringSeq<T>(this IEnumerable<T> self)
        {
            return $"[{string.Join(", ", self)}]";
        }

        public static T GetValueOrDefault<T>(this Dictionary<string, object> self, string key, T defaultValue)
        {
            object value;
            if (!self.TryGetValue(key, out value)) return defaultValue;
            if (!(value is T)) return defaultValue;
            return (T)value;
        }
    }
}