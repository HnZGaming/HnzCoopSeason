using System;
using System.Collections.Generic;
using HnzCoopSeason.MES;
using HnzCoopSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class MesGrid
    {
        public enum SpawningState
        {
            Idle,
            Spawning,
            Success,
            Failure,
        }

        readonly string _prefix;
        readonly bool _ignoreForDespawn;
        DateTime? _ignoreForDespawnStartTime;

        public MesGrid(string id, string prefix, bool ignoreForDespawn)
        {
            Id = id;
            _prefix = prefix;
            _ignoreForDespawn = ignoreForDespawn;
        }

        public string Id { get; }

        public SpawningState State { get; private set; }

        public IMyCubeGrid Grid { get; private set; }

        public event Action<IMyCubeGrid, bool> OnGridSet;
        public event Action<IMyCubeGrid> OnGridUnset;

        public void Load() // called once
        {
            MESApi.Instance.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);
        }

        public void Unload() // called once
        {
            if (State == SpawningState.Spawning)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} unloading while spawning");
            }

            MESApi.Instance.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);
            OnGridSet = null;
        }

        public void RequestSpawn(string spawnGroup, string mainPrefab, string factionTag, MatrixD targetMatrix) // called multiple times
        {
            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} spawning");

            if (Grid != null)
            {
                throw new InvalidOperationException("grid already set");
            }

            if (!MESApi.Instance.CustomSpawnRequest(new MESApi.CustomSpawnRequestArgs
                {
                    SpawnGroups = new List<string> { spawnGroup },
                    SpawningMatrix = targetMatrix,
                    IgnoreSafetyCheck = true,
                    SpawnProfileId = nameof(HnzCoopSeason),
                    FactionOverride = factionTag,
                    Context = new MesGridContext(Id, mainPrefab).ToXml(),
                }))
            {
                State = SpawningState.Failure;
                MyLog.Default.Error($"[HnzCoopSeason] MesGrid {Id} failed to spawn: '{spawnGroup}' at '{targetMatrix.Translation}'");
                return;
            }

            _ignoreForDespawnStartTime = null;
            State = SpawningState.Spawning;
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (State != SpawningState.Spawning) return;
            if (!IsMyGrid(grid, true)) return;

            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} spawn found");
            grid.DisplayName = $"{_prefix} {grid.DisplayName}";

            SetGrid(grid, false);
        }

        void SetGrid(IMyCubeGrid grid, bool recovery)
        {
            if (Grid != null)
            {
                throw new InvalidOperationException("grid already set");
            }

            if (!IsMyGrid(grid, true))
            {
                throw new InvalidOperationException($"invalid grid set; id: {Id}");
            }

            _ignoreForDespawnStartTime = DateTime.UtcNow + TimeSpan.FromSeconds(10);
            Grid = grid;
            State = SpawningState.Success;
            OnGridSet?.Invoke(Grid, recovery);
        }

        public bool TryRecover(IEnumerable<IMyCubeGrid> grids)
        {
            foreach (var g in grids)
            {
                if (IsMyGrid(g, true))
                {
                    SetGrid(g, true);
                    return true;
                }
            }

            return false;
        }

        public void CloseAllMyGrids(IEnumerable<IMyCubeGrid> grids)
        {
            foreach (var g in grids)
            {
                if (IsMyGrid(g, false))
                {
                    g.Close();
                    MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} closed on load");
                }
            }
        }

        public void Despawn()
        {
            if (State == SpawningState.Spawning)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} despawning while spawning");
            }

            if (Grid != null)
            {
                // despawn all grids except for player grids that may be attached to them
                var grids = new List<IMyCubeGrid>();
                Grid.GetGridGroup(GridLinkTypeEnum.Logical).GetGrids(grids);
                foreach (var g in grids)
                {
                    if (!VRageUtils.IsGridControlledByAI(g))
                    {
                        MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} skipped despawning: '{g.DisplayName}'");
                        NpcData.RemoveNpcData(g);
                        continue;
                    }

                    g.Close();
                    MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} despawned: '{g.DisplayName}'");
                }

                OnGridUnset?.Invoke(Grid);
                Grid = null;
            }

            State = SpawningState.Idle;
        }

        public void Update()
        {
            ValidateGridSpawned();
            ValidateIgnoreForDespawn();
        }

        void ValidateGridSpawned()
        {
            if (State != SpawningState.Success) return;
            if (!Grid.MarkedForClose && !Grid.Closed) return;

            MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} grid removed externally");
            OnGridUnset?.Invoke(Grid);
            Grid = null;
            State = SpawningState.Idle;
        }

        void ValidateIgnoreForDespawn()
        {
            if (State != SpawningState.Success) return;
            if (_ignoreForDespawnStartTime == null) return;
            if (_ignoreForDespawnStartTime.Value > DateTime.UtcNow) return;

            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} ignore for despawn: '{Grid.DisplayName}'");

            if (!MESApi.Instance.SetSpawnerIgnoreForDespawn(Grid, _ignoreForDespawn))
            {
                MyLog.Default.Error($"[HnzCoopSeason] failed to set ignore for despawn: '{Grid.DisplayName}', {MyAPIGateway.Session.GameplayFrameCounter}");
            }

            _ignoreForDespawnStartTime = null;
        }

        bool IsMyGrid(IMyCubeGrid grid, bool checkMainGrid)
        {
            if (grid == null) return false;

            NpcData npcData;
            if (!NpcData.TryGetNpcData(grid, out npcData)) return false;
            if (string.IsNullOrEmpty(npcData.Context)) return false;

            MesGridContext context;
            if (!MesGridContext.FromXml(npcData.Context, out context)) return false;
            if (context.Id != Id) return false;

            if (!checkMainGrid) return true;
            if (string.IsNullOrEmpty(context.MainPrefabId)) return true; // `MainPrefabId` wasn't specified upon spawning
            if (context.MainPrefabId == npcData.OriginalPrefabId) return true;

            return false;
        }
    }
}