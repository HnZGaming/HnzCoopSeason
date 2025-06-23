using HnzCoopSeason.HudUtils;
using HnzCoopSeason.Missions.Hud;
using VRage.Game.Entity.UseObject;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace HnzCoopSeason.Missions
{
    // example:
    // https://github.com/THDigi/SE-ModScript-Examples/blob/master/Data/Scripts/Examples/Example_CustomUseObject.cs

    [MyUseObject("coopcontract")]
    public class MissionBlockUseObject : MyUseObjectBase
    {
        public MissionBlockUseObject(IMyEntity owner, string dummyName, IMyModelDummy dummyData, uint shapeKey) : base(owner, dummyData)
        {
            // This class gets instanced per entity that has this detector useobject on it.
            // NOTE: this exact constructor signature is required, will throw errors mid-loading (and prevent world from loading) otherwise.
        }

        public override UseActionEnum PrimaryAction => UseActionEnum.OpenTerminal;
        public override UseActionEnum SecondaryAction => UseActionEnum.OpenTerminal;

        public override MyActionDescription GetActionInfo(UseActionEnum actionEnum)
        {
            return default(MyActionDescription);
        }

        public override void Use(UseActionEnum actionEnum, IMyEntity user)
        {
            MissionWindow.Instance.SetDisplayMode(HudDisplayMode.VisibleWithCursor);
        }
    }
}