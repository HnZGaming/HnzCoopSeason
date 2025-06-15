using System;
using System.Xml.Serialization;
using VRage.Serialization;
using HnzUtils;

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
        public SerializableDictionary<string, string> CustomData;

        public override string ToString()
        {
            return $"MissionConfig({nameof(Type)}: {Type}, {nameof(Goal)}: {Goal}, {nameof(CustomData)}: {CustomData?.Dictionary?.ToStringDic()})";
        }
    }
}