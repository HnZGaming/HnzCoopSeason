using System.Collections.Generic;

namespace HnzCoopSeason.Utils.Hud
{
    public sealed class HudElementStack
    {
        readonly List<HudElement> _elements = new List<HudElement>();

        public double Padding { private get; set; }
        public double Offset { private get; set; }

        public void Clear()
        {
            _elements.Clear();
        }

        public void Add(HudElement element)
        {
            _elements.Add(element);
        }

        public void Remove(HudElement element)
        {
            _elements.Remove(element);
        }

        public void Render(bool forceHide = false)
        {
            var offset = Offset;
            foreach (var e in _elements)
            {
                offset += e.Render(offset, forceHide) + Padding;
            }
        }
    }
}