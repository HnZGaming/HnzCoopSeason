using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils.Hud;

namespace HnzCoopSeason
{
    public sealed class ScreenTopView
    {
        public static readonly ScreenTopView Instance = new ScreenTopView();
        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        string _targetKey;

        public void Close()
        {
            _entries.Clear();
            _targetKey = null;
        }

        public void AddGroup(string key, HudElementStack group, int order)
        {
            _entries.Add(key, new Entry(group, order));
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

        void UpdateTarget()
        {
            _targetKey = _entries
                .Where(p => p.Value.Active)
                .OrderByDescending(p => p.Value.Order)
                .FirstOrDefault()
                .Key;
        }

        public void Render()
        {
            if (_targetKey == null) return; // shouldn't happen

            foreach (var kvp in _entries)
            {
                if (kvp.Key != _targetKey)
                {
                    kvp.Value.Stack.Render(forceHide: true);
                }
            }

            _entries[_targetKey].Stack.Render();
        }

        sealed class Entry
        {
            public readonly HudElementStack Stack;
            public readonly int Order;
            public bool Active = true;

            public Entry(HudElementStack stack, int order)
            {
                Stack = stack;
                Order = order;
            }
        }
    }
}