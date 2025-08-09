using System;
using System.Linq;
using System.Xml.Serialization;
using HnzCoopSeason.Spawners;
using HnzUtils;

namespace HnzCoopSeason.Orks
{
    [Serializable]
    public sealed class OrkConfig
    {
        [XmlAttribute]
        public int ProgressLevel = 1;

        [XmlAttribute]
        public SpawnType SpawnType = SpawnType.SpaceShip;

        [XmlAttribute]
        public float Weight = 1;

        [XmlElement("SpawnGroup")]
        public SpawnGroupConfig[] SpawnGroups = { new SpawnGroupConfig() };

        [XmlIgnore]
        public string[] SpawnGroupNames => SpawnGroups.Select(g => g.SpawnGroup).ToArray();

        public bool HasSpawnType(bool atmospheric)
        {
            return atmospheric
                ? SpawnType == SpawnType.AtmosphericShip
                : SpawnType != SpawnType.AtmosphericShip;
        }

        public override string ToString()
        {
            return $"MesEncounterConfig({nameof(ProgressLevel)}: {ProgressLevel}, {nameof(SpawnType)}: {SpawnType}, {nameof(Weight)}: {Weight}, {nameof(SpawnGroups)}: {SpawnGroups.ToStringSeq()})";
        }
    }
}