using System.Collections.Generic;

namespace HnzCoopSeason.HudUtils
{
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
            Entry entry;
            if (!_entries.TryGetValue(key, out entry)) return;

            entry.Active = active;
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
            string best = null;
            var bestOrder = 0;

            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (!entry.Active || !entry.Enabled) continue;
                if (best != null && entry.Order <= bestOrder) continue;

                best = pair.Key;
                bestOrder = entry.Order;
            }

            _targetKey = best;
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
