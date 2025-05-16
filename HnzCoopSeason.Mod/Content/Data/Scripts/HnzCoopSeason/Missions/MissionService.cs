using System;

namespace HnzCoopSeason.Missions
{
    public sealed class MissionService
    {
        public static readonly MissionService Instance = new MissionService();

        public event Action<Mission[]> OnMissionsUpdated;

        public void Load()
        {
        }

        public void Unload()
        {
        }

        public void OnConfigLoad()
        {
            OnMissionsUpdated?.Invoke(new[]
            {
                new Mission
                {
                    Id = 1,
                    Type = MissionType.Acquisition,
                    Title = "Acquisition Contract Title Acquisition Contract Title Acquisition Contract Title ",
                    Description = "Acquisition Contract Description Acquisition Contract Description Acquisition Contract Description ",
                    Progress = 1,
                    TotalProgress = 10,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
                new Mission
                {
                    Id = 2,
                    Type = MissionType.Acquisition,
                    Title = "Unironically, not enough stones",
                    Description = "Our pet bird just threw up due to a hangover the other day and she needs a bulk of stones to reset her gastroliths. Please collect as much as you can.",
                    Progress = 94485,
                    TotalProgress = 2400000,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
                new Mission
                {
                    Id = 3,
                    Type = MissionType.Acquisition,
                    Title = "Acquisition Contract Title Acquisition Contract Title Acquisition Contract Title ",
                    Description = "Acquisition Contract Description Acquisition Contract Description Acquisition Contract Description ",
                    Progress = 4,
                    TotalProgress = 10,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
            });
        }

        public void Submit(long missionId)
        {
        }
    }
}