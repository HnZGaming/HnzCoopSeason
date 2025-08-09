using System;
using System.Xml.Serialization;
using HnzCoopSeason.Spawners;

namespace HnzCoopSeason.Merchants
{
    [Serializable]
    public sealed class MerchantConfig
    {
        [XmlText]
        public string Prefab = "Economy_Outpost_1";

        [XmlAttribute]
        public SpawnType SpawnType = SpawnType.SpaceShip;

        public override string ToString()
        {
            return $"{nameof(Prefab)}: {Prefab}, {nameof(SpawnType)}: {SpawnType}";
        }
    }
}