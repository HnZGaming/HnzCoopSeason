using System;

namespace HnzPveSeason.Utils
{
    public sealed class Interval
    {
        DateTime _last;

        public void Initialize()
        {
            _last = DateTime.UtcNow;
        }

        public bool Update(TimeSpan span)
        {
            var now = DateTime.UtcNow;
            if (now - _last > span)
            {
                _last = now;
                return true;
            }

            return false;
        }
    }
}