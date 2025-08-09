using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Spawners;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Merchants
{
    public sealed class MerchantSpawner
    {
        static readonly Guid StorageKey = Guid.Parse("8e562067-5807-49a0-9d7d-108febcece97");
        readonly string _id;
        readonly MerchantConfig[] _configs;
        readonly IMyFaction _faction;

        public MerchantSpawner(string id, MerchantConfig[] configs)
        {
            _id = id;
            _configs = configs;
            _faction = MyAPIGateway.Session.Factions.TryGetFactionByTag("MERC");
        }

        public event Action<Merchant, bool> OnMerchantFound;
        public bool Spawning { get; private set; }

        public void TryFind(IMyCubeGrid[] grids)
        {
            // load existing grid
            var myGrids = grids.Where(g => IsMyGrid(g)).ToArray();
            for (var i = 0; i < myGrids.Length; i++)
            {
                if (i > 0) // shouldn't happen
                {
                    myGrids[i].Close();
                    MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_id} found multiple grids; closing except one");
                    continue;
                }

                MyLog.Default.Info($"[HnzCoopSeason] poi merchant {_id} recovering existing grid");
                OnGridFound(new List<IMyCubeGrid> { myGrids[i] }, null);
            }
        }

        bool IsMyGrid(IMyCubeGrid grid)
        {
            if (grid.Closed) return false;
            if (grid.MarkedForClose) return false;

            string value;
            if (!grid.TryGetStorageValue(StorageKey, out value)) return false;

            return value == _id;
        }

        public void TrySpawn(int configIndex, Vector3D position)
        {
            try
            {
                Spawning = true;

                var config = _configs[Math.Abs(configIndex) % _configs.Length];
                var matrixBuilder = new SpawnMatrixBuilder
                {
                    Sphere = new BoundingSphereD(position, SessionConfig.Instance.EncounterRadius),
                    Clearance = SessionConfig.Instance.EncounterClearance,
                    SnapToVoxel = config.SpawnType == SpawnType.PlanetaryStation,
                    Count = 1,
                    PlayerPosition = null,
                };

                if (!matrixBuilder.TryBuild())
                {
                    throw new InvalidOperationException($"[HnzCoopSeason] poi merchant {_id} failed to find position for spawning");
                }

                var matrix = matrixBuilder.Results[0];

                var resultGrids = new List<IMyCubeGrid>();
                MyAPIGateway.PrefabManager.SpawnPrefab(
                    resultList: resultGrids,
                    prefabName: config.Prefab,
                    position: matrix.Translation,
                    forward: matrix.Forward,
                    up: matrix.Up,
                    ownerId: _faction.FounderId,
                    spawningOptions: SpawningOptions.RotateFirstCockpitTowardsDirection,
                    callback: () => OnGridFound(resultGrids, config.SpawnType));
            }
            catch (Exception e)
            {
                Spawning = false;
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_id} failed to spawn: {e}");
            }
        }

        void OnGridFound(List<IMyCubeGrid> resultGrids, SpawnType? spawnType)
        {
            Spawning = false;

            if (resultGrids.Count == 0)
            {
                MyLog.Default.Error($"[HnzCoopSeason] poi merchant {_id} failed to spawn via SpawnPrefab()");
                return;
            }

            var grid = resultGrids[0];
            grid.UpdateStorageValue(StorageKey, _id);

            if (spawnType == SpawnType.PlanetaryStation)
            {
                AlignPlanetaryStation(grid);
            }

            if (spawnType != null)
            {
                grid.CustomName = $"[{_faction.Tag}] {grid.CustomName}";
            }

            var merchant = new Merchant(grid, _id);
            OnMerchantFound?.Invoke(merchant, spawnType != null);
        }

        static void AlignPlanetaryStation(IMyCubeGrid grid)
        {
            var blocks = grid.GetFatBlocks<IMyStoreBlock>();
            var alignmentBlock = blocks.FirstOrDefault();
            if (alignmentBlock == null)
            {
                MyLog.Default.Error($"[HnzCoopSeason] store block not found in grid; name: '{grid.DisplayName}'");
                return;
            }

            VRageUtils.AlignPlanetaryStation(grid, alignmentBlock);
        }
    }
}