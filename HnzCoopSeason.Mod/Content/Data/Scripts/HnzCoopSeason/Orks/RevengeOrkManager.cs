using System;
using System.Collections.Generic;
using HnzCoopSeason.Spawners;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Orks
{
    public sealed class RevengeOrkManager
    {
        public static readonly RevengeOrkManager Instance = new RevengeOrkManager();

        const string OrkFactionTag = "PORKS";

        const double NoAiSpawnMargin = 50;

        LinkedList<MesEncounter> _orks;
        List<IMyCubeGrid> _noAiGrids;
        int _increment;

        // sequential no-ai spawn state: one prefab per tick, each placed past the previous ship's measured bounds
        Queue<string> _noAiQueue;
        Vector3D _noAiOrigin;
        Vector3D _noAiAxis;
        double _noAiEdge;
        long _noAiOwnerId;
        int _spawnLevel = 1;

        public void Load()
        {
            _orks = new LinkedList<MesEncounter>();
            _noAiGrids = new List<IMyCubeGrid>();
        }

        void Clear(bool sessionUnload)
        {
            foreach (var ork in _orks)
            {
                ork.Unload(sessionUnload);
            }

            _orks.Clear();

            if (!sessionUnload)
            {
                foreach (var grid in _noAiGrids)
                {
                    if (grid != null && !grid.MarkedForClose) grid.Close();
                }

                // grids from before a restart aren't in the list; a stored hp multiplier marks them ork-spawned
                var entities = new HashSet<IMyEntity>();
                MyAPIGateway.Entities.GetEntities(entities, e => e is IMyCubeGrid);
                foreach (var entity in entities)
                {
                    if (entity.MarkedForClose) continue;
                    if (!OrkHpMultipliers.HasStored(entity)) continue;

                    MyLog.Default.Info($"[HnzCoopSeason] revenge despawn: closing restored ork grid '{((IMyCubeGrid)entity).CustomName}'");
                    entity.Close();
                }
            }

            _noAiGrids.Clear();
            _noAiQueue = null; // abort any in-flight sequential spawn
        }

        public void Unload()
        {
            Clear(true);
        }

        public void DespawnAll()
        {
            Clear(false);
        }

        public void Spawn(Vector3 position, string[] spawnGroupNames, int level, bool disarm = false)
        {
            // revenge orks have no poi; they fight at the level of the ork config they spawned from
            _spawnLevel = level;

            if (disarm)
            {
                // no-ai spawn: no MES involved at all -- direct prefab spawn owned by the ork faction,
                // so the grids sit in the hp-multiplier damage watcher without any behavior flying/taunting.
                SpawnNoAi(position, spawnGroupNames);
                return;
            }

            var ork = new MesEncounter($"revenge-ork-{_increment++}", position);
            _orks.AddLast(ork);

            ork.OnGridSet += OnGridSet; // cleared by the encounter's own Unload
            ork.Load(Array.Empty<IMyCubeGrid>());
            ork.ForceSpawn(spawnGroupNames);
        }

        void OnGridSet(IMyCubeGrid grid)
        {
            OrkHpMultipliers.Register(grid, OrkUtils.ComputeHpMultiplier(_spawnLevel));
        }

        void SpawnNoAi(Vector3D center, string[] spawnGroupNames)
        {
            var faction = MyAPIGateway.Session.Factions.TryGetFactionByTag(OrkFactionTag);
            if (faction == null)
            {
                MyLog.Default.Error($"[HnzCoopSeason] revenge no-ai spawn: faction not found: '{OrkFactionTag}'");
                return;
            }

            _noAiQueue = new Queue<string>();
            foreach (var groupName in spawnGroupNames)
            {
                MySpawnGroupDefinition group = null;
                foreach (var d in MyDefinitionManager.Static.GetSpawnGroupDefinitions())
                {
                    if (d.Id.SubtypeName == groupName)
                    {
                        group = d;
                        break;
                    }
                }

                if (group == null)
                {
                    MyLog.Default.Warning($"[HnzCoopSeason] revenge no-ai spawn: spawn group not found: '{groupName}'");
                    continue;
                }

                foreach (var prefab in group.Prefabs)
                {
                    _noAiQueue.Enqueue(prefab.SubtypeId);
                }
            }

            _noAiOwnerId = faction.FounderId;
            _noAiOrigin = center;
            _noAiAxis = Vector3D.Right;
            _noAiEdge = 0;
            SpawnNextNoAi();
        }

        void SpawnNextNoAi()
        {
            if (_noAiQueue == null || _noAiQueue.Count == 0) return;

            var prefabName = _noAiQueue.Dequeue();
            var def = MyDefinitionManager.Static.GetPrefabDefinition(prefabName);
            double radius = def != null && def.BoundingSphere.Radius > 0 ? def.BoundingSphere.Radius : 100;

            // place this ship's center one bounding-radius past the current free edge
            var position = _noAiOrigin + _noAiAxis * (_noAiEdge + NoAiSpawnMargin + radius);
            var fallbackEdge = _noAiEdge + NoAiSpawnMargin + radius * 2;
            var grids = new List<IMyCubeGrid>();
            MyAPIGateway.PrefabManager.SpawnPrefab(
                grids, prefabName, position, Vector3.Forward, Vector3.Up,
                Vector3.Zero, Vector3.Zero, null, SpawningOptions.None, _noAiOwnerId, false, () =>
                {
                    // measure what actually spawned and advance the free edge past its bounds
                    var edge = fallbackEdge;
                    foreach (var grid in grids)
                    {
                        _noAiGrids.Add(grid);
                        OnGridSet(grid);
                        var count = OrkUtils.DisarmGrid(grid);
                        MyLog.Default.Info($"[HnzCoopSeason] no-ai ork spawned: '{grid.CustomName}', disarmed blocks: {count}");

                        var aabb = grid.WorldAABB;
                        for (var i = 0; i < 8; i++)
                        {
                            edge = Math.Max(edge, Vector3D.Dot(aabb.GetCorner(i) - _noAiOrigin, _noAiAxis));
                        }
                    }

                    _noAiEdge = edge;
                    DeferTicks(10, SpawnNextNoAi);
                });
        }

        static void DeferTicks(int ticks, Action action)
        {
            if (ticks <= 0)
            {
                action();
                return;
            }

            MyAPIGateway.Utilities.InvokeOnGameThread(() => DeferTicks(ticks - 1, action));
        }
    }
}
