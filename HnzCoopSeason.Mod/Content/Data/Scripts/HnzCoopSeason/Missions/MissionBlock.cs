using System.Collections.Generic;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Missions
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ContractBlock), true, "CoopContractBlock")]
    public sealed class MissionBlock : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            MyLog.Default.Info("[HnzCoopSeason] contract block init");
        }

        public static bool TryFindNearby(IMyPlayer player, out MissionBlock missionBlock)
        {
            missionBlock = null;
            var character = player.Character;
            if (character == null) return false;

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

        public static bool IsMissionBlock(IMyEntity entity)
        {
            var block = entity as IMyFunctionalBlock;
            if (block == null) return false;

            return block.BlockDefinition.SubtypeId == "CoopContractBlock";
        }
    }
}