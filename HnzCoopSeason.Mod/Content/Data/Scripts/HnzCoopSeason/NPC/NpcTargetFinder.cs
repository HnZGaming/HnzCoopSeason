using System;
using System.Collections.Generic;
using GridStorage.API;
using HnzCoopSeason.HudUtils;
using HnzCoopSeason.POI;
using HnzCoopSeason.Spawners;
using HnzUtils;
using HnzUtils.Pools;
using MES;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace HnzCoopSeason.NPC
{
    /// <summary>Finds the NPC grid the player is aiming at and maintains the on-screen target reticles.</summary>
    public sealed class NpcTargetFinder
    {
        #region Fields

        const int MaxLineOfSightChecks = 32;
        const int MaxCachedContexts = 256;
        const double TargetSearchDistance = 10 * 1000; // meters

        // key: NPC grid EntityId
        static readonly Dictionary<long, CachedContext> ContextCache = new Dictionary<long, CachedContext>();
        static readonly List<TargetReticle.Target> ReticleTargets = new List<TargetReticle.Target>(TargetReticle.PoolSize);

        static readonly Pool<List<Candidate>> GridSearchPool =
            new Pool<List<Candidate>>(
                () => new List<Candidate>(),
                l => l.Clear());


        #endregion

        #region Struct

        public struct Result
        {
            public IMyCubeGrid Grid; // null when nothing is aimed at
            public bool IsBoss;
        }

        struct Analysis
        {
            public IMyCubeGrid Grid;
            public GridOwnerType Owner;
            public int SpawnGroupIndex;
            public string FactionTag;
        }

        struct Candidate
        {
            public double Weight; // aim priority, lowest first
            public int Order;
            public Analysis Analysis;
        }

        struct CachedContext
        {
            public string Raw; // a change invalidates the cached index
            public int SpawnGroupIndex;
        }

        #endregion

        #region API

        public static void RefreshTransforms()
        {
            TargetReticle.Update();
        }

        public static void Clear()
        {
            TargetReticle.Clear();
            ContextCache.Clear();
        }

        /// <summary>Rebuilds the reticle list and returns the aimed target (Grid==null if none); when inactive it only clears.</summary>
        public static Result Scan(bool active)
        {
            ReticleTargets.Clear();
            return active ? Find() : default(Result);
        }

        public static void PushReticles()
        {
            TargetReticle.SetTargets(CoopHud.ShowTargetReticle ? ReticleTargets : null);
        }

        #endregion

        #region Targeting

        static Result Find()
        {
            var player = MyAPIGateway.Session.Player;
            if (player == null) return default(Result);

            var character = player.Character;
            if (character == null) return default(Result);

            var camera = MyAPIGateway.Session.Camera;
            var characterPosition = character.WorldMatrix.Translation;

            var grids = GridSearchPool.Get();
            CollectCandidates(camera, characterPosition, grids);
            var target = SelectTarget(grids, camera, characterPosition);
            GridSearchPool.Release(grids);

            if (target.Grid == null) return default(Result);

            // a boss already has brackets
            var isBoss = IsBossOrk(ref target);
            if (!isBoss) TryAddReticle(ref target, target.Grid.WorldMatrix.Translation);

            return new Result { Grid = target.Grid, IsBoss = isBoss };
        }

        /// <summary>Queries grids in the view cone, keyed by aim priority (lowest first).</summary>
        static void CollectCandidates(IMyCamera camera, Vector3D characterPosition, List<Candidate> grids)
        {
            var coneRadians = MathHelper.ToRadians(MathHelper.Clamp(CoopHud.ReticleFov, 1f, 80f));
            var coneCosine = Math.Cos(coneRadians);
            var minBlocks = CoopHud.MinTargetBlocks;
            var aimRay = new RayD(camera.Position, camera.WorldMatrix.Forward);

            var entities = QueryGridsInView(camera);
            foreach (var entity in entities)
            {
                var grid = entity as IMyCubeGrid;
                if (grid == null) continue;
                if (VRageUtils.IsInAnySafeZone(grid.EntityId)) continue;
                if (grid.Physics == null) continue; // projection

                // debris filter
                var cubeGrid = grid as MyCubeGrid;
                if (minBlocks > 0 && cubeGrid != null && cubeGrid.BlocksCount < minBlocks) continue;

                var obbHit = new MyOrientedBoundingBoxD(grid.LocalAABB, grid.WorldMatrix).Intersects(ref aimRay);
                var aimDistance = obbHit.HasValue ? Math.Max(0, obbHit.Value) : 0;

                var gridPosition = grid.WorldMatrix.Translation;
                var dot = Vector3D.Dot((gridPosition - camera.WorldMatrix.Translation).Normalized(), camera.WorldMatrix.Forward);
                var offAxis = 1 - dot;
                var enclosing = grid.WorldAABB.Contains(characterPosition) != ContainmentType.Disjoint;

                if (!obbHit.HasValue)
                {
                    if (dot <= 0) continue;
                    if (!enclosing && !IsOnScreen(gridPosition)) continue;
                }

                var analysis = Analyze(grid);
                if (analysis.Owner == GridOwnerType.Player) continue; // non pvp
                if (analysis.FactionTag == "MERC") continue;

                if (IsBossOrk(ref analysis) && IsIntactConstruct(grid)) TryAddReticle(ref analysis, gridPosition);
                if (!obbHit.HasValue && dot < coneCosine && !enclosing) continue;

                grids.Add(new Candidate
                {
                    Weight = RankWeight(obbHit.HasValue, aimDistance, enclosing, offAxis),
                    Order = grids.Count,
                    Analysis = analysis,
                });
            }

            ListPool<MyEntity>.Instance.Release(entities);
            grids.Sort((a, b) => a.Weight != b.Weight ? a.Weight.CompareTo(b.Weight) : a.Order.CompareTo(b.Order));
        }

        /// <summary>Coarse OBB query of everything roughly in front of the camera</summary>
        static List<MyEntity> QueryGridsInView(IMyCamera camera)
        {
            var halfHeight = Math.Tan(camera.FovWithZoom * 0.5) * TargetSearchDistance;
            var viewport = camera.ViewportSize;

            var obb = new MyOrientedBoundingBoxD(
                camera.Position + camera.WorldMatrix.Forward * (TargetSearchDistance * 0.5),
                new Vector3D(halfHeight * (viewport.X / viewport.Y), halfHeight, TargetSearchDistance * 0.5),
                Quaternion.CreateFromRotationMatrix(camera.WorldMatrix));

            var entities = ListPool<MyEntity>.Instance.Get();
            MyGamePruningStructure.GetAllEntitiesInOBB(ref obb, entities, MyEntityQueryType.Both);
            return entities;
        }

        /// <summary>Sort key, lowest first. priority crosshair hit > enclosing > then off-axis angle.</summary>
        static double RankWeight(bool obbHit, double aimDistance, bool enclosing, double offAxis)
        {
            if (obbHit) return -1000 + aimDistance * 0.001;
            return enclosing ? -100 + offAxis : offAxis;
        }

        /// <summary>Walks candidates best-first and returns the first with line of sight</summary>
        static Analysis SelectTarget(List<Candidate> grids, IMyCamera camera, Vector3D characterPosition)
        {
            var nearestToCrosshair = default(Analysis);
            var losBudget = MaxLineOfSightChecks;
            var ranOutOfChecks = false;

            foreach (var candidate in grids)
            {
                var analysis = candidate.Analysis;
                if (nearestToCrosshair.Grid == null) nearestToCrosshair = analysis;

                if (losBudget-- <= 0)
                {
                    ranOutOfChecks = true;
                    break;
                }

                if (!HasLineOfSight(analysis.Grid, camera.Position, characterPosition)) continue;

                return analysis;
            }

            // fall back when the ray budget ran out
            return ranOutOfChecks ? nearestToCrosshair : default(Analysis);
        }

        #endregion

        #region Utils

        static void TryAddReticle(ref Analysis analysis, Vector3D gridPosition)
        {
            if (ReticleTargets.Count >= TargetReticle.PoolSize) return;

            var camera = MyAPIGateway.Session.Camera;
            var minDistance = CoopHud.ReticleMinDistance;
            if (minDistance > 0 && Vector3D.Distance(camera.Position, gridPosition) < minDistance) return;

            var enclosing = analysis.Grid.WorldAABB.Contains(camera.Position) != ContainmentType.Disjoint;
            if (!enclosing && !IsOnScreen(gridPosition)) return;

            ReticleTargets.Add(new TargetReticle.Target
            {
                EntityId = analysis.Grid.EntityId,
                Grid = analysis.Grid,
                Position = GetReticlePosition(analysis.Grid),
                LocalBox = analysis.Grid.LocalAABB,
                WorldMatrix = analysis.Grid.WorldMatrix,
                IsBoss = IsBossOrk(ref analysis),
            });
        }

        static bool IsBossOrk(ref Analysis analysis)
        {
            // on a DS client, the server sends the boss EntityId in the POI marker
            if (analysis.Grid != null && PoiMapView.IsBossGrid(analysis.Grid.EntityId)) return true;

            // doesn't work on DS client
            return analysis.SpawnGroupIndex == 0 && analysis.FactionTag == "PORKS";
        }

        static bool IsOnScreen(Vector3D position)
        {
            var screen = MyAPIGateway.Session.Camera.WorldToScreen(ref position);
            return Math.Abs(screen.X) <= 1 && Math.Abs(screen.Y) <= 1;
        }

        /// <summary>Whether the grid is reachable without terrain in the way; only voxels count as cover.</summary>
        static bool HasLineOfSight(IMyCubeGrid grid, Vector3D from, Vector3D characterPosition)
        {
            // standing inside it
            if (grid.WorldAABB.Contains(characterPosition) != ContainmentType.Disjoint) return true;

            // CastRay, not CastLongRay: the long variant ignores voxels
            IHitInfo hit;
            MyAPIGateway.Physics.CastRay(from, grid.WorldAABB.Center, out hit);

            return !(hit?.HitEntity is IMyVoxelBase);
        }

        /// <summary>Whether this is the boss ship rather than wreckage split off it.</summary>
        static bool IsIntactConstruct(IMyCubeGrid grid)
        {
            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(grid, out state)) return false;
            return state.Controllers.Length > 0;
        }

        static Analysis Analyze(IMyCubeGrid grid)
        {
            var analysis = default(Analysis);
            analysis.Grid = grid;
            analysis.SpawnGroupIndex = -1;

            var ownerId = grid.BigOwners.GetElementAtOrDefault(0, 0);
            analysis.Owner = VRageUtils.GetOwnerType(ownerId);
            analysis.FactionTag = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId)?.Tag;
            analysis.SpawnGroupIndex = GetSpawnGroupIndex(grid);

            return analysis;
        }

        /// <summary>Spawn-group index, cached by raw blob so a pre-MES-write read re-resolves later.</summary>
        static int GetSpawnGroupIndex(IMyCubeGrid grid)
        {
            string raw;
            if (!NpcData.TryGetRawData(grid, out raw) || string.IsNullOrEmpty(raw)) return -1;

            CachedContext cached;
            if (ContextCache.TryGetValue(grid.EntityId, out cached) && cached.Raw == raw) return cached.SpawnGroupIndex;

            MesGridContext context;
            var index = MesGridGroup.TryGetSpawnContext(grid, out context) ? context.Index : -1;

            if (ContextCache.Count >= MaxCachedContexts) PruneContextCache();
            ContextCache[grid.EntityId] = new CachedContext { Raw = raw, SpawnGroupIndex = index };
            return index;
        }

        static void PruneContextCache()
        {
            var stale = new List<long>();
            foreach (var pair in ContextCache)
            {
                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(pair.Key, out entity) && !entity.MarkedForClose) continue;
                stale.Add(pair.Key);
            }

            foreach (var id in stale)
            {
                ContextCache.Remove(id);
            }

            // clear all if still full
            if (ContextCache.Count >= MaxCachedContexts) ContextCache.Clear();
        }

        static Vector3D GetReticlePosition(IMyCubeGrid grid)
        {
            return grid.WorldAABB.Center;
        }

        #endregion
    }
}
