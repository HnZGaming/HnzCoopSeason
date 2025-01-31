using System;
using System.Xml.Serialization;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class MesStaticEncounterConfig
    {
        [XmlAttribute]
        public string SpawnGroup = "Orks-SpawnGroup-Boss-KillaKrooZa";

        [XmlAttribute]
        public float SpawnRadius = 10000;

        [XmlAttribute]
        public float ClearanceRadius = 1000;

        [XmlAttribute]
        public float Weight = 1;
    }
}