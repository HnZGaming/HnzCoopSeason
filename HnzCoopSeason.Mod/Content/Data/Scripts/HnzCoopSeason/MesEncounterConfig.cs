using System;
using System.Xml.Serialization;
using HnzCoopSeason.Utils;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class MesEncounterConfig
    {
        [XmlAttribute]
        public int ProgressLevel = 1;

        [XmlAttribute]
        public SpawnType SpawnType = SpawnType.SpaceShip;

        [XmlAttribute]
        public float Weight = 1;

        [XmlElement("SpawnGroup")]
        public MesEncounterSpawnGroupConfig[] SpawnGroups = { new MesEncounterSpawnGroupConfig() };

        public override string ToString()
        {
            return $"MesEncounterConfig({nameof(ProgressLevel)}: {ProgressLevel}, {nameof(SpawnType)}: {SpawnType}, {nameof(Weight)}: {Weight}, {nameof(SpawnGroups)}: {SpawnGroups.ToStringSeq()})";
        }
    }
}