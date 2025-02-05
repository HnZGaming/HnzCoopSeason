using System;
using System.Xml.Serialization;
using VRage.Game.ObjectBuilders.Definitions;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class StoreItemConfig
    {
        [XmlAttribute]
        public StoreItemTypes Type = StoreItemTypes.Offer;

        [XmlAttribute]
        public string ItemDefinitionId = "MyObjectBuilder_Component/Tech2x";

        [XmlAttribute]
        public int Amount = 10;

        [XmlAttribute]
        public int PricePerUnit = 10;
    }
}