using System;
using System.Collections.Generic;
using HnzPveSeason.MES;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
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

        string _spawnGroup;

        public MesGrid(string id)
        {
            Id = id;
        }

        public string Id { get; }

        public SpawningState State { get; private set; }

        public IMyCubeGrid Grid { get; private set; }

        public event Action<IMyCubeGrid> OnGridSet;
        public event Action<IMyCubeGrid> OnGridUnset;

        public void Load() // called once
        {
            MESApi.Instance.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);
        }

        public void Unload() // called once
        {
            if (State == SpawningState.Spawning)
            {
                MyLog.Default.Warning($"[HnzPveSeason] MesGrid `{Id}` unloading while spawning");
            }

            MESApi.Instance.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);
            OnGridSet = null;
        }

        public void RequestSpawn(string spawnGroup, MatrixD targetMatrix) // called multiple times
        {
            MyLog.Default.Info($"[HnzPveSeason] MesGrid `{Id}` spawning");

            if (Grid != null)
            {
                throw new InvalidOperationException("grid already set");
            }

            if (!MESApi.Instance.CustomSpawnRequest(new MESApi.CustomSpawnRequestArgs
                {
                    SpawnGroups = new List<string> { spawnGroup },
                    SpawningMatrix = targetMatrix,
                    IgnoreSafetyCheck = true,
                    SpawnProfileId = nameof(HnzPveSeason),
                    Context = Id,
                }))
            {
                State = SpawningState.Failure;
                MyLog.Default.Error($"[HnzPveSeason] MesGrid `{Id}` failed to spawn: '{spawnGroup}' at '{targetMatrix.Translation}'");
                return;
            }

            _spawnGroup = spawnGroup;
            State = SpawningState.Spawning;
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (!IsMyGrid(grid)) return;

            MyLog.Default.Info($"[HnzPveSeason] MesGrid `{Id}` spawn found");
            SetGrid(grid);
        }

        void SetGrid(IMyCubeGrid grid)
        {
            if (Grid != null)
            {
                throw new InvalidOperationException("grid already set");
            }

            if (!IsMyGrid(grid))
            {
                throw new InvalidOperationException($"invalid grid set; id: `{Id}`");
            }

            Grid = grid;
            State = SpawningState.Success;
            OnGridSet?.Invoke(Grid);
        }

        public bool TryRecover(IEnumerable<IMyCubeGrid> grids)
        {
            foreach (var g in grids)
            {
                if (IsMyGrid(g))
                {
                    SetGrid(g);
                    return true;
                }
            }

            return false;
        }

        public void Despawn()
        {
            if (State == SpawningState.Spawning)
            {
                MyLog.Default.Warning($"[HnzPveSeason] MesGrid `{Id}` despawning while spawning");
            }

            if (Grid != null)
            {
                Grid?.Close();
                OnGridUnset?.Invoke(Grid);
                Grid = null;
            }

            State = SpawningState.Idle;
        }

        public void Update()
        {
            ValidateGridSpawned();
        }

        void ValidateGridSpawned()
        {
            if (State != SpawningState.Success) return;
            if (!Grid.MarkedForClose && !Grid.Closed) return;

            MyLog.Default.Warning($"[HnzPveSeason] MesGrid `{Id}` grid removed externally");
            OnGridUnset?.Invoke(Grid);
            Grid = null;
            State = SpawningState.Idle;
        }

        bool IsMyGrid(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            NpcData npcData;
            if (!NpcData.TryGetNpcData(grid, out npcData)) return false;
            if (npcData.SpawnGroupName != _spawnGroup) return false;
            if (npcData.Context != Id) return false;

            return true;
        }
    }
}