using System;
using System.Xml.Serialization;

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
        public bool Planetary;

        [XmlAttribute]
        public bool SnapToVoxel;

        [XmlAttribute]
        public float Area = 10000;

        [XmlAttribute]
        public float Clearance = 1000;

        [XmlAttribute]
        public float Weight = 1;

        public override string ToString()
        {
            return $"{nameof(SpawnGroup)}: {SpawnGroup}, {nameof(Planetary)}: {Planetary}, {nameof(SnapToVoxel)}: {SnapToVoxel}, {nameof(Area)}: {Area}, {nameof(Clearance)}: {Clearance}, {nameof(Weight)}: {Weight}";
        }
    }
}