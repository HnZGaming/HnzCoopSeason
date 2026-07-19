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

        // damage taken by ork blocks is divided by this, lerped from Start to End as progress
        // crosses this level's span (e.g. level 5 of 5 spans 80%..100%); 1 or less = no reduction
        [XmlAttribute]
        public float OrksDamageReductionScaleStart = 1f;

        [XmlAttribute]
        public float OrksDamageReductionScaleEnd = 1f;

        public ProgressionLevelConfig()
        {
        }

        public ProgressionLevelConfig(int level, int minPlayerCount, float scaleStart, float scaleEnd)
        {
            Level = level;
            MinPlayerCount = minPlayerCount;
            OrksDamageReductionScaleStart = scaleStart;
            OrksDamageReductionScaleEnd = scaleEnd;
        }
    }
}
