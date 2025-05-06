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
                    Type = MissionType.Acquisition,
                    Title = "Acquisition Contract Title",
                    Description = "Acquisition Contract Description",
                    Progress = 1,
                    TotalProgress = 10,
                },
                new Mission
                {
                    Type = MissionType.Acquisition,
                    Title = "Acquisition Contract Title",
                    Description = "Acquisition Contract Description",
                    Progress = 1,
                    TotalProgress = 10,
                },
                new Mission
                {
                    Type = MissionType.Acquisition,
                    Title = "Acquisition Contract Title",
                    Description = "Acquisition Contract Description",
                    Progress = 1,
                    TotalProgress = 10,
                },
            });
        }
    }
}