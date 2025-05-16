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
        public long Id;

        [XmlElement]
        public string Title;

        [XmlElement]
        public string Description;

        [XmlElement]
        public long Progress;

        [XmlElement]
        public long TotalProgress;

        [XmlElement]
        public string AcquisitionItemType;

        [XmlIgnore]
        public double ProgressPercentage => (double)Progress / TotalProgress * 100;
    }
}