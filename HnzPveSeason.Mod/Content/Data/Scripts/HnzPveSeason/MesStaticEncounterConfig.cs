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
        public SpawnEnvironment Environment = SpawnEnvironment.Space;

        [XmlAttribute]
        public float SpawnRadius = 10000;

        [XmlAttribute]
        public float ClearanceRadius = 1000;

        [XmlAttribute]
        public float Weight = 1;

        public bool IsSpaceSpawn()
        {
            return Environment == SpawnEnvironment.Space;
        }

        public bool IsPlanetSpawn()
        {
            return Environment == SpawnEnvironment.PlanetOrbit ||
                   Environment == SpawnEnvironment.PlanetSurface;
        }
    }
}