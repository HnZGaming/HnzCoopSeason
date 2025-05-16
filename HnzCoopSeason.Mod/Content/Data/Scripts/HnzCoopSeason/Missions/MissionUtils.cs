using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

namespace HnzCoopSeason.Missions
{
    public static class MissionUtils
    {
        public const string MissionBlockFar = "You must find the contract block";

        public static bool TryGetMissionBlockNearby(IMyEntity character, out MissionBlock missionBlock)
        {
            missionBlock = null;

            var sphere = new BoundingSphereD(character.GetPosition(), 5);
            var result = new List<MyEntity>();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, result, MyEntityQueryType.Static);
            if (result.Count == 0) return false;

            foreach (var entity in result)
            {
                if (IsMissionBlock(entity))
                {
                    missionBlock = entity.GameLogic.GetAs<MissionBlock>();
                    return true;
                }
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