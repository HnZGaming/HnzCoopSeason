using System.Collections.Generic;
using HudAPI;

namespace HnzCoopSeason.Utils.ScreenSpace
{
    public sealed class StackView
    {
        public static readonly StackView Instance = new StackView();
        readonly List<ElementGroup> _elementGroups = new List<ElementGroup>();
        HudAPIv2 _api;

        StackView()
        {
            _api = new HudAPIv2();
        }

        public void Close()
        {
            foreach (var eg in _elementGroups)
            {
                eg.Clear();
            }

            _elementGroups.Clear();
            _api = null;
        }

        public void AddGroup(ElementGroup group)
        {
            _elementGroups.Add(group);
        }

        public void RemoveGroup(ElementGroup group)
        {
            _elementGroups.Remove(group);
        }

        public void Render()
        {
            var offset = 0d;
            var forceRender = false;
            foreach (var eg in _elementGroups)
            {
                if (forceRender || eg.IsDirty())
                {
                    offset += eg.Render(offset);
                    forceRender = true;
                }
            }
        }
    }
}