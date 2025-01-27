using VRage.Game.Components;
using VRage.Utils;

namespace HnzPveSeason
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once UnusedType.Global
    public sealed class Session : MySessionComponentBase
    {
        public override void LoadData()
        {
            base.LoadData();

            MyLog.Default.Info("[HnzPveSeason] session loaded");
        }
    }
}