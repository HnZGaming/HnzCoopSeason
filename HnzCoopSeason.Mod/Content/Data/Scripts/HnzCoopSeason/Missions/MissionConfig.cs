using System;
using System.Xml.Serialization;

namespace HnzCoopSeason.Missions
{
    [Serializable]
    public sealed class MissionConfig
    {
        [XmlElement]
        public MissionType Type;

        [XmlElement]
        public string Title;

        [XmlElement]
        public string Description;

        [XmlElement]
        public int Goal;

        [XmlElement]
        public string AcquisitionItemType;

        public override string ToString()
        {
            return $"MissionConfig({nameof(Type)}: {Type}, {nameof(Title)}: {Title}, {nameof(Description)}: {Description}, {nameof(Goal)}: {Goal}, {nameof(AcquisitionItemType)}: {AcquisitionItemType})";
        }
    }
}