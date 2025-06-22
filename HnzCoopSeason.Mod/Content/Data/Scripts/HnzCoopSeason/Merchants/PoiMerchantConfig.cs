using System;
using System.Xml.Serialization;
using HnzCoopSeason.Spawners;

namespace HnzCoopSeason.Merchants
{
    [Serializable]
    public sealed class PoiMerchantConfig
    {
        [XmlText]
        public string Prefab = "Economy_Outpost_1";

        [XmlAttribute]
        public SpawnType SpawnType = SpawnType.SpaceShip;

        [XmlAttribute]
        public float OffsetY = 0f;

        public override string ToString()
        {
            return $"{nameof(Prefab)}: {Prefab}, {nameof(SpawnType)}: {SpawnType}, {nameof(OffsetY)}: {OffsetY}";
        }
    }
}