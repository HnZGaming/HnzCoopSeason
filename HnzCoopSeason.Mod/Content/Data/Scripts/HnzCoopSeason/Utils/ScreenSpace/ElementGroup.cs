using System.Collections.Generic;

namespace HnzCoopSeason.Utils.ScreenSpace
{
    public sealed class ElementGroup
    {
        readonly List<Element> _elements = new List<Element>();

        public double Padding { private get; set; }
        public double Offset { private get; set; }

        public void Clear()
        {
            foreach (var e in _elements)
            {
                e.Clear();
            }

            _elements.Clear();
        }

        public void Add(Element element)
        {
            _elements.Add(element);
        }

        public void Remove(Element element)
        {
            _elements.Remove(element);
        }

        public double Render(double offset)
        {
            offset += Offset;
            foreach (var e in _elements)
            {
                offset += e.Render(offset) + Padding;
                e.IsDirty = false;
            }

            return offset;
        }

        public bool IsDirty()
        {
            foreach (var e in _elements)
            {
                if (e.IsDirty)
                {
                    return true;
                }
            }

            return false;
        }
    }
}