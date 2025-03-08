using System;
using System.Xml.Serialization;
using HnzCoopSeason.Utils;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class PoiMerchantConfig
    {
        [XmlAttribute]
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