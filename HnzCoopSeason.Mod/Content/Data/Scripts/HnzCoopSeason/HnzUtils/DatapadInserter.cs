using System.Linq;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzUtils
{
    public sealed class DatapadInserter
    {
        public delegate bool DataFactory(IMyCubeGrid grid, out string data);

        readonly string _datapadName;
        DataFactory _dataFactory;

        public DatapadInserter(string datapadName)
        {
            _datapadName = datapadName;
        }

        public void Load(DataFactory dataFactory)
        {
            MyVisualScriptLogicProvider.RespawnShipSpawned += OnRespawnShipSpawned;
            _dataFactory = dataFactory;
        }

        public void Unload()
        {
            MyVisualScriptLogicProvider.RespawnShipSpawned -= OnRespawnShipSpawned;
            _dataFactory = null;
        }

        void OnRespawnShipSpawned(long shipEntityId, long playerId, string respawnShipPrefabName)
        {
            MyLog.Default.Info("[HnzUtils] inserting a datapad to cockpit");

            var grid = (IMyCubeGrid)MyAPIGateway.Entities.GetEntityById(shipEntityId);

            // vanilla game only inserts datapad to planetary respawn pods (I don't know why)
            var position = grid.GetPosition();
            var isPlanetary = VRageUtils.CalculateNaturalGravity(position) != Vector3.Zero;
            if (!isPlanetary) return;

            string data;
            if (!_dataFactory(grid, out data)) return;

            var cockpit = grid.GetFatBlocks<IMyCockpit>().First();
            cockpit.GetInventory(0).AddItems(1, new MyObjectBuilder_Datapad
            {
                SubtypeName = "Datapad",
                Name = _datapadName,
                Data = data,
            });
        }
    }
}