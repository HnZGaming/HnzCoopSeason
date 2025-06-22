using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.NPC;
using HnzUtils;
using MES;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Spawners
{
    public sealed class MesGridGroup
    {
        public enum SpawningState
        {
            Idle,
            Spawning,
            Success,
            Failure,
        }

        readonly Dictionary<int, IMyCubeGrid> _allGrids;

        public MesGridGroup(string id)
        {
            Id = id;
            _allGrids = new Dictionary<int, IMyCubeGrid>();
        }

        public string Id { get; }

        public SpawningState State { get; private set; }

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
                    MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} closing safely on load");
                    g.Close();
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

        public void RequestSpawn(IReadOnlyList<string> spawnGroups, SpawnMatrixBuilder matrixBuilder) // called multiple times
        {
            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} spawning; group: {spawnGroups.ToStringSeq()}, position: {matrixBuilder.Sphere.Center}");

            if (_allGrids.Any())
            {
                MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} despawning existing grids");
                Despawn();
            }

            for (var i = 0; i < spawnGroups.Count; i++)
            {
                var success = MESApi.Instance.CustomSpawnRequest(new MESApi.CustomSpawnRequestArgs
                {
                    SpawnGroups = new List<string> { spawnGroups[i] },
                    SpawningMatrix = matrixBuilder.Results[i],
                    IgnoreSafetyCheck = true,
                    SpawnProfileId = nameof(HnzCoopSeason),
                    Context = new MesGridContext(Id, i).ToXml(),
                });

                if (!success)
                {
                    MyLog.Default.Error($"[HnzCoopSeason] MesGrid {Id} failed to spawn: '{spawnGroups[i]}' ({i})");

                    if (i == 0) // main grid
                    {
                        State = SpawningState.Failure;
                        return;
                    }
                }
            }

            State = SpawningState.Spawning;
        }

        void OnMesAnySuccessfulSpawn(IMyCubeGrid grid)
        {
            MesGridContext context;
            if (!TryGetMyContext(grid, out context)) return;

            MyLog.Default.WriteLine($"[HnzCoopSeason] MesGrid {Id} grid spawned; index: {context.Index}");

            grid.OnClosing += OnGridClosing;
            _allGrids.Add(context.Index, grid);

            MESApi.Instance.RegisterDespawnWatcher(grid, OnGridDespawningByMes);

            if (context.Index == 0)
            {
                State = SpawningState.Success;
                OnMainGridSet?.Invoke(grid);
            }
        }

        void OnGridDespawningByMes(IMyCubeGrid grid, string cause)
        {
            MesGridContext context;
            if (!TryGetMyContext(grid, out context))
            {
                MyLog.Default.Error("[HnzCoopSeason] context not found; shouldn't happen");
                return;
            }

            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} grid despawned by MES; index: {context.Index}, cause: {cause}");
        }

        void Despawn()
        {
            if (State == SpawningState.Spawning)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} despawning while spawning");
            }

            foreach (var kvp in _allGrids)
            {
                // `OnGridClosing()` will be called
                CloseGridSafely(kvp.Value);
            }

            _allGrids.Clear();
            MyLog.Default.Error($"[HnzCoopSeason] MesGrid {Id} all grids clear");
        }

        void OnGridClosing(IMyEntity entity)
        {
            var grid = (IMyCubeGrid)entity;

            MesGridContext context;
            if (!TryGetMyContext(grid, out context))
            {
                MyLog.Default.Error("[HnzCoopSeason] context not found; shouldn't happen");
                return;
            }

            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} grid closing; index: {context.Index}");

            grid.OnClosing -= OnGridClosing;
            _allGrids.Remove(context.Index);

            if (context.Index == 0) // main grid
            {
                OnMainGridUnset?.Invoke(grid);
                State = SpawningState.Idle;
            }
        }

        bool TryGetMyContext(IMyCubeGrid grid, out MesGridContext context)
        {
            return TryGetSpawnContext(grid, out context) && context.Id == Id;
        }

        public static bool TryGetSpawnContext(IMyCubeGrid grid, out MesGridContext context)
        {
            context = null;
            if (grid == null) return false;

            NpcData npcData;
            if (!NpcData.TryGetNpcData(grid, out npcData)) return false;
            if (string.IsNullOrEmpty(npcData.Context)) return false;

            if (!MesGridContext.FromXml(npcData.Context, out context)) return false;
            return true;
        }

        void CloseGridSafely(IMyCubeGrid grid)
        {
            if (grid.Closed) return;
            if (grid.MarkedForClose) return;

            // despawn all grids except for player grids that may be attached to them
            var grids = new List<IMyCubeGrid>();
            grid.GetGridGroup(GridLinkTypeEnum.Logical).GetGrids(grids);
            foreach (var g in grids)
            {
                var analysis = CoopGrids.Analyze(g);
                if (analysis.Owner == CoopGrids.Owner.Player)
                {
                    MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} not despawned: '{g.CustomName}'");
                    NpcData.RemoveNpcData(g);
                    continue;
                }

                MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} despawning safely: '{g.CustomName}'");
                g.Close();
            }
        }

        public override string ToString()
        {
            return $"MesGrid({nameof(Id)}: {Id}, {nameof(State)}: {State}, {nameof(_allGrids)}: {_allGrids.Values.Select(g => g.CustomName).ToStringSeq()})";
        }
    }
}