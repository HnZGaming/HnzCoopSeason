using System.Collections.Generic;
using System.Linq;

namespace HnzCoopSeason.HudUtils
{
    // arbiter for the single screen-top meter slot; highest-priority active state wins
    public sealed class ScreenTopHud
    {
        public static readonly ScreenTopHud Instance = new ScreenTopHud();
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        string _targetKey;

        public void Close()
        {
            _entries.Clear();
            _targetKey = null;
        }

        public void AddGroup(string key, MeterState state, int order)
        {
            _entries.Add(key, new Entry(state, order));
            UpdateTarget();
        }

        public void RemoveGroup(string key)
        {
            _entries.Remove(key);
            UpdateTarget();
        }

        public void SetActive(string key, bool active)
        {
            _entries[key].Active = active;
            UpdateTarget();
        }

        public void SetEnabled(string key, bool enabled) // config gate, independent of gameplay activity
        {
            Entry entry;
            if (!_entries.TryGetValue(key, out entry)) return;

            entry.Enabled = enabled;
            UpdateTarget();
        }

        public MeterState Current
        {
            get
            {
                if (_targetKey == null) return null;

                Entry entry;
                return _entries.TryGetValue(_targetKey, out entry) ? entry.State : null;
            }
        }

        void UpdateTarget()
        {
            _targetKey = _entries
                .Where(p => p.Value.Active && p.Value.Enabled)
                .OrderByDescending(p => p.Value.Order)
                .FirstOrDefault()
                .Key;
        }

        sealed class Entry
        {
            public readonly MeterState State;
            public readonly int Order;
            public bool Active = true;
            public bool Enabled = true;

            public Entry(MeterState state, int order)
            {
                State = state;
                Order = order;
            }
        }
    }
}
