using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class NpcHud
    {
        public static readonly NpcHud Instance = new NpcHud();

        struct Analysis
        {
            public IMyCubeGrid Grid;
            public GridOwnerType Owner;
            public int SpawnGroupIndex;
            public string FactionTag;
        }

        static readonly Pool<SortedList<double, Analysis>> GridSearchPool =
            new Pool<SortedList<double, Analysis>>(
                () => new SortedList<double, Analysis>(),
                l => l.Clear());

        const int MaxLineOfSightChecks = 32;
        const int MaxCachedContexts = 256;

        struct CachedContext
        {
            public string Raw; // a change invalidates the cached index
            public int SpawnGroupIndex;
        }

        static readonly Dictionary<long, CachedContext> ContextCache = new Dictionary<long, CachedContext>();

        static readonly List<TargetReticle.Target> ReticleTargets = new List<TargetReticle.Target>(TargetReticle.PoolSize);

        MeterState _state;

        public void Load()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            _state = new MeterState
            {
                BarName = "CAPMETER",
                ShowInfoIcon = true,
            };

            ScreenTopHud.Instance.AddGroup(nameof(NpcHud), _state, 1);
        }

        public void Unload()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            TargetReticle.Clear();
            ContextCache.Clear();
            ScreenTopHud.Instance.RemoveGroup(nameof(NpcHud));
            _state = null;
        }

        public void Update()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            TargetReticle.RefreshTransforms();

            if (MyAPIGateway.Session.GameplayFrameCounter % 5 != 0) return;

            ReticleTargets.Clear();
            var canRender = !CoopHud.YieldToWeaponCore && TryApplyHudElements();
            ScreenTopHud.Instance.SetActive(nameof(NpcHud), canRender);
            TargetReticle.SetTargets(CoopHud.ShowTargetReticle ? ReticleTargets : null);
        }

        bool TryApplyHudElements() // false if deactivating the view
        {
            var player = MyAPIGateway.Session.Player;
            if (player == null) return false;

            var character = player.Character;
            if (character == null) return false;

            var camera = MyAPIGateway.Session.Camera;
            const double distance = 10 * 1000;

            var characterPosition = character.WorldMatrix.Translation;

            var coneRadians = MathHelper.ToRadians(MathHelper.Clamp(CoopHud.ReticleFov, 1f, 80f));
            var coneCosine = Math.Cos(coneRadians);

            var halfHeight = Math.Tan(camera.FovWithZoom * 0.5) * distance;
            var viewport = camera.ViewportSize;
            var obb = new MyOrientedBoundingBoxD(
                camera.Position + camera.WorldMatrix.Forward * (distance * 0.5),
                new Vector3D(halfHeight * (viewport.X / viewport.Y), halfHeight, distance * 0.5),
                Quaternion.CreateFromRotationMatrix(camera.WorldMatrix));

            var result = ListPool<MyEntity>.Instance.Get();
            MyGamePruningStructure.GetAllEntitiesInOBB(ref obb, result, MyEntityQueryType.Both);

            var minBlocks = CoopHud.MinTargetBlocks;

            var aimRay = new RayD(camera.Position, camera.WorldMatrix.Forward);

            var grids = GridSearchPool.Get();
            foreach (var entity in result)
            {
                var grid = entity as IMyCubeGrid;
                if (grid == null) continue;
                if (VRageUtils.IsInAnySafeZone(grid.EntityId)) continue;
                if (grid.Physics == null) continue; // projection

                // debris filter: a split keeps the parent's takeover state
                var cubeGrid = grid as MyCubeGrid;
                if (minBlocks > 0 && cubeGrid != null && cubeGrid.BlocksCount < minBlocks) continue;

                // a camera inside the box yields a negative tmin, hence the clamp
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

                if (!obbHit.HasValue && dot < coneCosine && !enclosing) continue; // capmeter candidates only

                // ranking, lowest first: crosshair hit, then enclosing, then off-axis angle
                var weight = obbHit.HasValue
                    ? -1000 + aimDistance * 0.001
                    : enclosing
                        ? -100 + offAxis
                        : offAxis;

                grids[weight] = analysis;
            }

            ListPool<MyEntity>.Instance.Release(result);

            var target = default(Analysis);
            var nearestToCrosshair = default(Analysis);
            var losBudget = MaxLineOfSightChecks;
            var ranOutOfChecks = false;

            foreach (var candidate in grids.Values)
            {
                if (nearestToCrosshair.Grid == null) nearestToCrosshair = candidate;

                if (losBudget-- <= 0)
                {
                    ranOutOfChecks = true;
                    break;
                }

                if (!HasLineOfSight(candidate.Grid, camera.Position, characterPosition)) continue;

                target = candidate;
                break;
            }

            // only fall back when the budget ran out; occluded candidates show nothing
            if (target.Grid == null && ranOutOfChecks) target = nearestToCrosshair;

            GridSearchPool.Release(grids);

            if (target.Grid == null) return false;

            // a boss already has brackets from the overlay above
            var isBoss = IsBossOrk(ref target);
            if (!isBoss) TryAddReticle(ref target, target.Grid.WorldMatrix.Translation);

            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(target.Grid, out state)) return false;

            var playerGroup = CoopGridTakeover.GetPlayerGroup(player.IdentityId);
            var takeoverReady = state.CanTakeOver && (state.TakeoverPlayerGroup == 0 || state.TakeoverPlayerGroup == playerGroup);

            // count what is still hostile; grinding a controller drops it from the array
            var remaining = state.Controllers.Count(id => id != 0 && id != playerGroup);

            // high-water mark, so removing a block doesn't renormalize the bar
            var takeoverTargetCount = Math.Max(state.MaxControllers, state.Controllers.Length);

            _state.Title = target.Grid.CustomName;
            _state.Subtitle = isBoss ? "This is the boss Ork! Neutralize it to reclaim the trading hub!" : "";
            _state.SubtitleHighlight = isBoss ? "boss Ork" : null;
            _state.Description = !takeoverReady
                ? "To neutralize a wild grid, take over its remote blocks and control seats."
                : "You can capture a neutralized grid into a garage block.";
            _state.Progress = takeoverTargetCount == 0 ? 0 : (double)remaining / takeoverTargetCount;
            _state.ValueText = $"{remaining}/{takeoverTargetCount}";
            _state.HealthStyle = true;
            _state.CompleteText = takeoverReady ? "READY TO CAPTURE" : null;

            return true;
        }

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
            // the server tells us; the spawn-group index below is a same-frame fallback that only
            // resolves in single player, since MES writes its context after the grid replicates
            if (analysis.Grid != null && PoiMapView.IsBossGrid(analysis.Grid.EntityId)) return true;

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
            // 0 is a real index (the boss), so -1 means no context
            analysis.SpawnGroupIndex = -1;

            var ownerId = grid.BigOwners.GetElementAtOrDefault(0, 0);
            analysis.Owner = VRageUtils.GetOwnerType(ownerId);
            analysis.FactionTag = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId)?.Tag;

            analysis.SpawnGroupIndex = GetSpawnGroupIndex(grid);

            return analysis;
        }

        /// <summary>Spawn-group index, cached on the raw blob so a read before MES writes it isn't cached forever.</summary>
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

            if (ContextCache.Count >= MaxCachedContexts) ContextCache.Clear();
        }

        static Vector3D GetReticlePosition(IMyCubeGrid grid)
        {
            return grid.WorldAABB.Center;
        }
    }
}
