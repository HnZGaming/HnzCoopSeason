using System;
using System.Xml.Serialization;
using HnzCoopSeason.Utils;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class MesStaticEncounterConfig
    {
        [XmlAttribute]
        public string SpawnGroup = "Orks-SpawnGroup-Boss-KillaKrooZa";

        [XmlAttribute]
        public string MainPrefab = ""; // in case a spawn group consists of multiple grids

        [XmlAttribute]
        public SpawnType SpawnType = SpawnType.SpaceShip;

        [XmlAttribute]
        public float Weight = 1;

        [XmlAttribute]
        public float MinProgress = 0;

        [XmlAttribute]
        public float MaxProgress = 1;

        public override string ToString()
        {
            return $"{nameof(SpawnGroup)}: {SpawnGroup}, {nameof(MainPrefab)}: {MainPrefab}, {nameof(SpawnType)}: {SpawnType}, {nameof(Weight)}: {Weight}, {nameof(MinProgress)}: {MinProgress}, {nameof(MaxProgress)}: {MaxProgress}";
        }
    }
}