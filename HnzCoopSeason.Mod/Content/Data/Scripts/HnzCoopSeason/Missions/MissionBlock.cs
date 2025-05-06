using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.ObjectBuilders;
using VRage.Utils;

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
    }
}