using System;
using System.Xml.Serialization;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class StoreItemConfig
    {
        [XmlAttribute]
        public string Type = "Component";

        [XmlAttribute]
        public string Subtype = "Tech2x";

        [XmlAttribute]
        public int PricePerUnit = 200000;

        [XmlAttribute]
        public int MinAmountPerUpdate = 5;

        [XmlAttribute]
        public int MaxAmountPerUpdate = 10;

        [XmlAttribute]
        public int MaxAmount = 100;

        public override string ToString()
        {
            return $"{nameof(Type)}: {Type}, {nameof(Subtype)}: {Subtype}, {nameof(PricePerUnit)}: {PricePerUnit}, {nameof(MinAmountPerUpdate)}: {MinAmountPerUpdate}, {nameof(MaxAmountPerUpdate)}: {MaxAmountPerUpdate}, {nameof(MaxAmount)}: {MaxAmount}";
        }
    }
}