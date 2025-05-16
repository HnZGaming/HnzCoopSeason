using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

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
                    Title = "Acquisition Contract Title Acquisition Contract Title Acquisition Contract Title ",
                    Description = "Acquisition Contract Description Acquisition Contract Description Acquisition Contract Description ",
                    Progress = 1,
                    TotalProgress = 10,
                },
                new Mission
                {
                    Type = MissionType.Acquisition,
                    Title = "Acquisition Contract Title Acquisition Contract Title Acquisition Contract Title ",
                    Description = "Acquisition Contract Description Acquisition Contract Description Acquisition Contract Description ",
                    Progress = 1,
                    TotalProgress = 10,
                },
                new Mission
                {
                    Type = MissionType.Acquisition,
                    Title = "Acquisition Contract Title Acquisition Contract Title Acquisition Contract Title ",
                    Description = "Acquisition Contract Description Acquisition Contract Description Acquisition Contract Description ",
                    Progress = 1,
                    TotalProgress = 10,
                },
            });
        }

        public static bool CanSubmit()
        {
            var character = MyAPIGateway.Session.LocalHumanPlayer?.Character;
            if (character == null) return false;

            var sphere = new BoundingSphereD(character.GetPosition(), 5);
            var result = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, result, MyEntityQueryType.Static);
            if (result.Count == 0) return false;

            foreach (var entity in result)
            {
                if (IsMissionBlock(entity)) return true;
            }

            return false;
        }

        static bool IsMissionBlock(IMyEntity entity)
        {
            var block = entity as IMyFunctionalBlock;
            if (block == null) return false;

            return block.BlockDefinition.SubtypeId == "CoopContractBlock";
        }
    }
}