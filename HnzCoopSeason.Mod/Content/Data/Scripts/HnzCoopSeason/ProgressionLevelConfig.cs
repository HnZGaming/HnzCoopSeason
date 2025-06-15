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

        public ProgressionLevelConfig()
        {
        }

        public ProgressionLevelConfig(int level, int minPlayerCount)
        {
            Level = level;
            MinPlayerCount = minPlayerCount;
        }
    }
}