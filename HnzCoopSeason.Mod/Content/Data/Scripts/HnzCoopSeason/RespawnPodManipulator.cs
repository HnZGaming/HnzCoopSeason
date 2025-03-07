using System.Linq;
using HnzCoopSeason.Utils;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public static class RespawnPodManipulator
    {
        public static void Load()
        {
            MyVisualScriptLogicProvider.RespawnShipSpawned += OnRespawnShipSpawned;
        }

        public static void Unload()
        {
            MyVisualScriptLogicProvider.RespawnShipSpawned -= OnRespawnShipSpawned;
        }

        static void OnRespawnShipSpawned(long shipEntityId, long playerId, string respawnShipPrefabName)
        {
            MyLog.Default.Info("[HnzCoopSeason] inserting a datapad to cockpit");

            var grid = (IMyCubeGrid)MyAPIGateway.Entities.GetEntityById(shipEntityId);
            var cockpit = grid.GetFatBlocks<IMyCockpit>().First();

            Vector3D coord;
            if (!Session.Instance.TryGetClosestPoiPosition(grid.PositionComp.GetPosition(), out coord))
            {
                MyLog.Default.Warning("[HnzCoopSeason] POI not found for datapad");
                return;
            }

            var gps = VRageUtils.FormatGps("Something", coord, "FFFFFF");
            cockpit.GetInventory(0).AddItems(1, new MyObjectBuilder_Datapad
            {
                SubtypeName = "Datapad",
                Name = "COOP",
                Data = string.Format(SessionConfig.Instance.RespawnDatapadTextFormat, gps),
            });
        }
    }
}