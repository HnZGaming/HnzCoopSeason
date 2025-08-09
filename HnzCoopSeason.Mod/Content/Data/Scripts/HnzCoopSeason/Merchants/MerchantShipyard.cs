using System;
using System.Linq;
using FlashGps;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Merchants
{
    public sealed class MerchantShipyard
    {
        const string BlockSubtypeId = "MES-Blocks-ShipyardTerminal";
        const string ProfileKey = "88334d52-3f3b-47cb-83c7-426fbc0553fa";
        const string ProfileValue = "MERC-Shipyard-Profile";
        const int SignalDurationSecs = 10;

        readonly IMyProjector _block;

        public static bool TryFind(IMyCubeGrid grid, out MerchantShipyard shipyard)
        {
            var block = grid.GetFatBlocks<IMyProjector>().FirstOrDefault(b => b.BlockDefinition.SubtypeId == BlockSubtypeId);
            if (block == null)
            {
                MyLog.Default.Error("[HnzCoopSeason] shipyard not found");
                shipyard = null;
                return false;
            }

            shipyard = new MerchantShipyard(block);
            return true;
        }

        MerchantShipyard(IMyProjector block)
        {
            _block = block;
        }

        public void Load()
        {
            _block.UpdateStorageValue(Guid.Parse(ProfileKey), ProfileValue);
        }

        public void Update()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 * SignalDurationSecs != 0) return;

            FlashGpsApi.Send(new FlashGpsApi.Entry
            {
                Id = _block.EntityId,
                EntityId = _block.EntityId,
                Name = "- Shipyard Block -\n    Buying grids!",
                Position = _block.GetPosition(),
                Color = Color.Cyan,
                Duration = SignalDurationSecs,
                Radius = 1000,
                Mute = true,
                Description = "Shipyard block allows you to sell grids for space credits"
            });
        }
    }
}