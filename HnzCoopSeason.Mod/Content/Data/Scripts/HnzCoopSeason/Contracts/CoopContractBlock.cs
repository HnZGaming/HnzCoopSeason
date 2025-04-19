using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace HnzCoopSeason.Contracts
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ContractBlock), true, "CoopContractBlock")]
    public sealed class CoopContractBlock : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            MyLog.Default.Info("[HnzCoopSeason] contract block init");
        }

        public void Use(IMyCharacter character) // client; called when the block's panel interacts with player
        {
            //if (character != MyAPIGateway.Session.Player.Character) return;

            MyLog.Default.Info($"[HnzCoopSeason] coopcontract; user: '{character.DisplayName}'");
            TextEditor.Instance.Visible = true;
        }
    }
}