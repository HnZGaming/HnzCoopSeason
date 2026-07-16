using System;
using System.Xml.Serialization;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class ProgressionLevelConfig
    {
        [XmlAttribute]
        public int Level;

        [XmlAttribute]
        public int MinPlayerCount;

        // effective hp multiplier against ork blocks, lerped from Start to End as progress
        // crosses this level's span (e.g. level 5 of 5 spans 80%..100%); 1 or less = no reduction
        [XmlAttribute]
        public float HpMultiplierStart = 1f;

        [XmlAttribute]
        public float HpMultiplierEnd = 1f;

        public ProgressionLevelConfig()
        {
        }

        public ProgressionLevelConfig(int level, int minPlayerCount, float hpStart, float hpEnd)
        {
            Level = level;
            MinPlayerCount = minPlayerCount;
            HpMultiplierStart = hpStart;
            HpMultiplierEnd = hpEnd;
        }
    }
}
