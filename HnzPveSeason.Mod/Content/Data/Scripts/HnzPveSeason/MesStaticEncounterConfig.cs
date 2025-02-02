using System;
using System.Xml.Serialization;
using HnzPveSeason.Utils;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class MesStaticEncounterConfig
    {
        [XmlAttribute]
        public string SpawnGroup = "Orks-SpawnGroup-Boss-KillaKrooZa";

        [XmlAttribute]
        public bool Planetary;

        [XmlAttribute]
        public bool SnapToVoxel;

        [XmlAttribute]
        public float SpawnRadius = 10000;

        [XmlAttribute]
        public float ClearanceRadius = 1000;

        [XmlAttribute]
        public float Weight = 1;

        public override string ToString()
        {
            return $"{nameof(SpawnGroup)}: {SpawnGroup}, {nameof(Planetary)}: {Planetary}, {nameof(SnapToVoxel)}: {SnapToVoxel}, {nameof(SpawnRadius)}: {SpawnRadius}, {nameof(ClearanceRadius)}: {ClearanceRadius}, {nameof(Weight)}: {Weight}";
        }
    }
}