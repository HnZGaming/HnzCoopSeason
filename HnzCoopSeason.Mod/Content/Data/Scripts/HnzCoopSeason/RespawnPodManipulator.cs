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
            var position = grid.GetPosition();
            var isPlanetary = VRageUtils.CalculateNaturalGravity(position) != Vector3.Zero;

            var closestPoi = Session.Instance.GetAllPois()
                .Where(p => p.IsPlanetary == isPlanetary)
                .OrderBy(p => Vector3D.Distance(p.Position, position))
                .FirstOrDefault();
            
            if (closestPoi == null)
            {
                MyLog.Default.Warning("[HnzCoopSeason] POI not found for datapad");
                return;
            }

            var gps = VRageUtils.FormatGps("Something", closestPoi.Position, "FFFFFF");
            cockpit.GetInventory(0).AddItems(1, new MyObjectBuilder_Datapad
            {
                SubtypeName = "Datapad",
                Name = "COOP",
                Data = string.Format(SessionConfig.Instance.RespawnDatapadTextFormat, gps),
            });
        }
    }
}