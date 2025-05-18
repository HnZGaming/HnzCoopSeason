using System;
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
        public int Progress;

        [ProtoMember(7)]
        public int Goal;

        [ProtoMember(8)]
        public string AcquisitionItemType;

        [ProtoIgnore]
        public double ProgressPercentage => (double)Progress / Goal * 100;
        
        [ProtoIgnore]
        public int RemainingProgress => Goal - Progress;

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
            Goal = config.Goal;
            AcquisitionItemType = config.AcquisitionItemType;
        }
    }
}