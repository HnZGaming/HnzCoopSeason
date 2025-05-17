using System;
using System.Xml.Serialization;
using ProtoBuf;

namespace HnzCoopSeason.Missions
{
    [ProtoContract]
    public sealed class Mission
    {
        [ProtoMember(1)]
        public MissionType Type;

        [ProtoMember(2)]
        public int Level;

        [ProtoMember(3)]
        public int Id;

        [ProtoMember(4)]
        public string Title;

        [ProtoMember(5)]
        public string Description;

        [ProtoMember(6)]
        [XmlElement]
        public int Progress;

        [ProtoMember(7)]
        public int TotalProgress;

        [ProtoMember(8)]
        public string AcquisitionItemType;

        [ProtoIgnore]
        public double ProgressPercentage => (double)Progress / TotalProgress * 100;

        public Mission()
        {
        }

        public Mission(MissionConfig config, int level, int id, int progress)
        {
            Type = config.Type;
            Level = level;
            Id = id;
            Title = config.Title;
            Description = config.Description;
            Progress = progress;
            TotalProgress = config.TotalProgress;
            AcquisitionItemType = config.AcquisitionItemType;
        }
    }
}