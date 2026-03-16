using System;
using System.Xml.Serialization;

namespace HnzCoopSeason.Merchants
{
    [Serializable]
    public sealed class StoreItemConfig
    {
        [XmlAttribute]
        public string Type = "Component";

        [XmlAttribute]
        public string Subtype = "Tech2x";

        [XmlAttribute]
        public int PricePerUnit;

        [XmlAttribute]
        public int AmountPerUpdate;

        [XmlAttribute]
        public int MaxAmount;

        public override string ToString()
        {
            return $"{nameof(Type)}: {Type}, {nameof(Subtype)}: {Subtype}, {nameof(PricePerUnit)}: {PricePerUnit}, {nameof(AmountPerUpdate)}: {AmountPerUpdate}, {nameof(MaxAmount)}: {MaxAmount}";
        }
    }
}