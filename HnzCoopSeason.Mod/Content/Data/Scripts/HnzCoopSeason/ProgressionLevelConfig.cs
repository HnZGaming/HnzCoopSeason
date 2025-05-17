using System;
using System.Xml.Serialization;
using HnzCoopSeason.Missions;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class ProgressionLevelConfig
    {
        [XmlAttribute]
        public int Level;

        [XmlAttribute]
        public int MinPlayerCount;

        [XmlArray]
        [XmlArrayItem("Mission")]
        public MissionConfig[] Missions;

        public ProgressionLevelConfig()
        {
        }

        public ProgressionLevelConfig(int level, int minPlayerCount, MissionConfig[] missions)
        {
            Level = level;
            MinPlayerCount = minPlayerCount;
            Missions = missions;
        }
    }
}