using System;
using System.Xml.Serialization;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class MesEncounterSpawnGroupConfig
    {
        [XmlText]
        public string SpawnGroup = "Orks-SpawnGroup-Boss-KillaKrooZa";

        public override string ToString()
        {
            return $"MesEncounterSpawnGroupConfig({nameof(SpawnGroup)}: {SpawnGroup})";
        }
    }
}