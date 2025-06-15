using ProtoBuf;
using VRage.Serialization;

namespace HnzCoopSeason.Missions
{
    [ProtoContract]
    public sealed class Mission
    {
        public static readonly Mission None = new Mission { Type = MissionType.None };

        [ProtoMember(1)]
        public MissionType Type;

        [ProtoMember(2)]
        public int Index;

        [ProtoMember(3)]
        public string Title;

        [ProtoMember(4)]
        public string Description;

        [ProtoMember(5)]
        public int Goal;

        // snapshot for clients;
        // shouldn't be consumed by server as potentially outdated
        [ProtoMember(6)]
        public int Progress;

        [ProtoMember(7)]
        public SerializableDictionary<string, string> CustomData;

        public Mission()
        {
        }

        public Mission(MissionConfig config, int index)
        {
            Type = config.Type;
            Index = index;
            Title = config.Title;
            Description = config.Description;
            Goal = config.Goal;
            CustomData = config.CustomData ?? new SerializableDictionary<string, string>();
        }

        [ProtoIgnore]
        public double ProgressPercentage => (double)Progress / Goal * 100;

        [ProtoIgnore]
        public int RemainingProgress => Goal - Progress;
    }
}