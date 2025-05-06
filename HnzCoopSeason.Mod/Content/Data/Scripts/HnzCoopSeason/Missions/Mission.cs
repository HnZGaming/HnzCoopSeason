using System;
using System.Xml.Serialization;

namespace HnzCoopSeason.Missions
{
    [Serializable]
    public sealed class Mission
    {
        [XmlElement]
        public MissionType Type;

        [XmlElement]
        public string Title;

        [XmlElement]
        public string Description;

        [XmlElement]
        public int Progress;

        [XmlElement]
        public int TotalProgress;
    }
}