using System.Collections.Generic;

namespace HnzPveSeason.Utils
{
    public static class LangUtils
    {
        public static string ToStringSeq<T>(this IEnumerable<T> self)
        {
            return $"[{string.Join(", ", self)}]";
        }
    }
}