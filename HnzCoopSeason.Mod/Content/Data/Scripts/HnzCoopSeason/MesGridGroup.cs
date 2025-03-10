using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.MES;
using HnzCoopSeason.Utils;
using Sandbox.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
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

        public void RequestSpawn(IReadOnlyList<string> spawnGroups, MatrixD targetMatrix, float clearance) // called multiple times
        {
            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} spawning; group: {spawnGroups.ToStringSeq()}, position: {targetMatrix.Translation}");

            if (_allGrids.Any())
            {
                MyLog.Default.Warning($"[HnzCoopSeason] MesGrid {Id} despawning existing grids");
                Despawn();
            }

            for (var i = 0; i < spawnGroups.Count; i++)
            {
                var spawnGroup = spawnGroups[i];

                // draws a circle around the up vector
                var matrix = CreateMatrix(targetMatrix, clearance, i, spawnGroups.Count);
                MyVisualScriptLogicProvider.AddGPS("Spawn", "", matrix.Translation, Color.Green, 10);

                var success = MESApi.Instance.CustomSpawnRequest(new MESApi.CustomSpawnRequestArgs
                {
                    SpawnGroups = new List<string> { spawnGroup },
                    SpawningMatrix = matrix,
                    IgnoreSafetyCheck = true,
                    SpawnProfileId = nameof(HnzCoopSeason),
                    Context = new MesGridContext(Id, i).ToXml(),
                });

                if (!success)
                {
                    MyLog.Default.Error($"[HnzCoopSeason] MesGrid {Id} failed to spawn: '{spawnGroup}' at '{targetMatrix.Translation}'");

                    if (i == 0) // main grid
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

            grid.OnClosing += OnGridClosing;
            _allGrids.Add(context.Index, grid);

            MyLog.Default.Info($"[HnzCoopSeason] MesGrid {Id} grid spawned; index: {context.Index}");

            if (context.Index == 0)
            {
                State = SpawningState.Success;
                OnMainGridSet?.Invoke(grid);
            }
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
        }

        void OnGridClosing(IMyEntity entity)
        {
            var grid = (IMyCubeGrid)entity;

            MesGridContext context;
            if (!TryGetMyContext(grid, out context))
            {
                throw new InvalidOperationException("context not found; shouldn't happen");
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

        static void CloseGridSafely(IMyCubeGrid grid)
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

        public override string ToString()
        {
            return $"MesGrid({nameof(Id)}: {Id}, {nameof(State)}: {State}, {nameof(_allGrids)}: {_allGrids.Values.Select(g => g.CustomName).ToStringSeq()})";
        }
    }
}