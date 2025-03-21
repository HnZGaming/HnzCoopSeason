using System;
using System.Xml.Serialization;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class SpawnGroupConfig
    {
        [XmlText]
        public string SpawnGroup = "Orks-SpawnGroup-Boss-KillaKrooZa";

        public override string ToString()
        {
            return $"SpawnGroupConfig({nameof(SpawnGroup)}: {SpawnGroup})";
        }
    }
}