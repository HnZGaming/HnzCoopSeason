using System;
using System.Collections.Generic;
using HnzCoopSeason.MES;
using HnzCoopSeason.Utils;
using VRage.Game.ModAPI;
using VRage.ModAPI;
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
        readonly HashSet<IMyCubeGrid> _allGrids;

        public MesGrid(string id, string prefix)
        {
            Id = id;
            _prefix = prefix;
            _allGrids = new HashSet<IMyCubeGrid>();
        }

        public string Id { get; }

        public SpawningState State { get; private set; }

        public IMyCubeGrid MainGrid { get; private set; }

        public event Action<IMyCubeGrid> OnMainGridSet;
        public event Action<IMyCubeGrid> OnMainGridUnset;

        public void Load(IEnumerable<IMyCubeGrid> grids) // called once
        {
            MESApi.Instance.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, true);

            // close all existing grids that belong to my ID
            foreach (var g in grids)
            {
                MesGridContext context;
                if (TryGetMyContext(g, out context))
                {
                    g.Close();
                    MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} closed on load");
                }
            }
        }

        public void Unload(bool sessionUnload) // called once
        {
            if (State == SpawningState.Spawning)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} unloading while spawning");
            }

            MESApi.Instance.RegisterSuccessfulSpawnAction(OnMesAnySuccessfulSpawn, false);
            OnMainGridSet = null;

            if (sessionUnload) return; // otherwise fails to unload session

            Despawn();
        }

        public void RequestSpawn(IReadOnlyList<string> spawnGroups, string factionTag, MatrixD targetMatrix) // called multiple times
        {
            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} spawning; group: {spawnGroups.ToStringSeq()}, {VRageUtils.FormatGps("Spawn", targetMatrix.Translation, "FFFFFF")}");

            if (MainGrid != null)
            {
                throw new InvalidOperationException("grids already spawned");
            }

            for (var i = 0; i < spawnGroups.Count; i++)
            {
                var isMainSpawn = i == 0;
                var spawnGroup = spawnGroups[i];
                var success = MESApi.Instance.CustomSpawnRequest(new MESApi.CustomSpawnRequestArgs
                {
                    SpawnGroups = new List<string> { spawnGroup },
                    SpawningMatrix = CreateMatrix(targetMatrix, 300, i, spawnGroups.Count),
                    IgnoreSafetyCheck = true,
                    SpawnProfileId = nameof(HnzCoopSeason),
                    FactionOverride = factionTag,
                    Context = new MesGridContext(Id, isMainSpawn).ToXml(),
                });

                if (!success)
                {
                    MyLog.Default.Error($"[HnzCoopSeason] MesGrid {Id} failed to spawn: '{spawnGroup}' at '{targetMatrix.Translation}'");

                    if (isMainSpawn)
                    {
                        State = SpawningState.Failure;
                        return;
                    }
                }
            }

            State = SpawningState.Spawning;
        }

        static MatrixD CreateMatrix(MatrixD center, float radius, int index, int count)
        {
            if (index == 0) return center;

            var step = MathHelper.TwoPi / count;
            var angle = index * step;
            var offset = (center.Right * Math.Cos(angle) + center.Forward * Math.Sin(angle)) * radius;

            center.Translation += offset;
            return center;
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            if (State != SpawningState.Spawning) return;

            MesGridContext context;
            if (!TryGetMyContext(grid, out context)) return;

            grid.CustomName = $"{_prefix} {grid.CustomName}";
            grid.OnClosing += OnGridClosing;
            _allGrids.Add(grid);

            if (!context.IsMainSpawn) return;

            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} main grid found");

            if (MainGrid != null)
            {
                throw new InvalidOperationException($"main grid already set: {Id}");
            }

            MainGrid = grid;
            State = SpawningState.Success;
            OnMainGridSet?.Invoke(MainGrid);
        }

        void Despawn()
        {
            if (State == SpawningState.Spawning)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} despawning while spawning");
            }

            foreach (var grid in _allGrids)
            {
                // `OnGridClosed()` will be called
                SafeCloseGrid(grid);
            }

            _allGrids.Clear();
        }

        void OnGridClosing(IMyEntity entity)
        {
            var grid = (IMyCubeGrid)entity;
            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} grid closing");

            grid.OnClosing -= OnGridClosing;
            _allGrids.Remove(grid);

            MesGridContext context;
            if (!TryGetMyContext(grid, out context))
            {
                throw new InvalidOperationException("context not found; shouldn't happen");
            }

            if (context.IsMainSpawn)
            {
                MainGrid = null;
                OnMainGridUnset?.Invoke(grid);
            }
        }

        public void Update()
        {
        }

        bool TryGetMyContext(IMyCubeGrid grid, out MesGridContext context)
        {
            context = null;
            if (grid == null) return false;

            NpcData npcData;
            if (!NpcData.TryGetNpcData(grid, out npcData)) return false;
            if (string.IsNullOrEmpty(npcData.Context)) return false;

            if (!MesGridContext.FromXml(npcData.Context, out context)) return false;
            return context.Id == Id;
        }

        static void SafeCloseGrid(IMyCubeGrid grid)
        {
            if (grid.Closed) return;
            if (grid.MarkedForClose) return;

            // despawn all grids except for player grids that may be attached to them
            var grids = new List<IMyCubeGrid>();
            grid.GetGridGroup(GridLinkTypeEnum.Logical).GetGrids(grids);
            foreach (var g in grids)
            {
                if (!VRageUtils.IsGridControlledByAI(g))
                {
                    MyLog.Default.Info($"[HnzCoopSeason] not despawned: '{g.CustomName}'");
                    NpcData.RemoveNpcData(g);
                    continue;
                }

                g.Close();
                MyLog.Default.Info($"[HnzCoopSeason] despawned: '{g.CustomName}'");
            }
        }
    }
}