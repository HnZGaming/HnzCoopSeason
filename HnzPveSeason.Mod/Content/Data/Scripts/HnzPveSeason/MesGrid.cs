using System;
using System.Collections.Generic;
using HnzPveSeason.MES;
using HnzPveSeason.Utils;
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

        static readonly Guid ModStorageKey = Guid.Parse("8e562067-5807-49a0-9d7d-108febcece97");
        const float TimeoutSecs = 10;

        string _spawnGroup;
        MatrixD _targetMatrix;
        DateTime _startTime;

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

            if (!MESApi.Instance.CustomSpawnRequest(
                    new List<string> { spawnGroup },
                    targetMatrix,
                    Vector3.Zero,
                    true,
                    null,
                    nameof(HnzPveSeason)))
            {
                State = SpawningState.Failure;
                MyLog.Default.Error($"[HnzPveSeason] MesGrid `{Id}` failed to spawn: '{spawnGroup}' at '{targetMatrix.Translation}'");
                return;
            }

            _spawnGroup = spawnGroup;
            _startTime = DateTime.UtcNow;
            _targetMatrix = targetMatrix;
            State = SpawningState.Spawning;
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (!HasMySpawnGroup(grid)) return;

            MyLog.Default.Info($"[HnzPveSeason] MesGrid `{Id}` spawn found");

            // todo modify MES to pass ID string to the spawn method
            var gridPos = grid.WorldMatrix.Translation;
            if (Vector3D.Distance(gridPos, _targetMatrix.Translation) > 500)
            {
                MyLog.Default.Warning($"[HnzPveSeason] MesGrid `{Id}` different position");
                return;
            }

            grid.UpdateStorageValue(ModStorageKey, Id);
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
            ValidateGridSpawning();
            ValidateGridSpawned();
        }

        void ValidateGridSpawning()
        {
            if (State != SpawningState.Spawning) return;

            var timeout = _startTime + TimeSpan.FromSeconds(TimeoutSecs) - DateTime.UtcNow;
            if (timeout.TotalSeconds > 0) return;

            MyLog.Default.Error($"[HnzPveSeason] MesGrid `{Id}` timeout");
            State = SpawningState.Failure;
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

        bool HasMySpawnGroup(IMyCubeGrid grid)
        {
            if (grid == null) return false;

            NpcData npcData;
            return NpcData.TryGetNpcData(grid, out npcData) && npcData.SpawnGroupName == _spawnGroup;
        }

        bool IsMyGrid(IMyCubeGrid grid)
        {
            string existingId;
            if (!grid.TryGetStorageValue(ModStorageKey, out existingId)) return false;
            if (existingId != Id) return false;
            return true;
        }
    }
}