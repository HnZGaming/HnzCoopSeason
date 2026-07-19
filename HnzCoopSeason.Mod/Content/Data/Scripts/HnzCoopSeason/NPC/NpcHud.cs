using System;
using System.Collections.Generic;
using System.Linq;
using GridStorage.API;
using HnzCoopSeason.HudUtils;
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

        // brackets used to be hidden inside 100m on the theory that the ship is plainly visible by
        // then; that is now a setting, defaulting to off (see CoopHud.HudConfig.ReticleMinDistance)
        // raycasts spent per update rejecting occluded candidates. deliberately generous: a
        // crosshair inside an asteroid field routinely has a dozen candidates with every one of
        // them behind rock, and stopping early there means falling back to an occluded target --
        // which is exactly the wall-hack this check exists to stop. at ~0.002ms a ray the whole
        // budget costs less than the entity query it follows
        const int MaxLineOfSightChecks = 32;
        const int MaxCachedContexts = 256; // prune above this; a busy view is a few dozen grids

        struct CachedContext
        {
            public string Raw; // the blob the index was parsed from; a change invalidates it
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
                ShowInfoIcon = true, // capmeter titles are grid names; the icon marks them as target info
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

            // every frame: keep the brackets on the target. the selection below is what costs
            // (query + context parse + raycasts) and is what the 5-frame gate exists for; re-reading
            // the transforms of grids already chosen is ~25x cheaper and needs no gate
            TargetReticle.RefreshTransforms();

            if (MyAPIGateway.Session.GameplayFrameCounter % 5 != 0) return;

            ReticleTargets.Clear();
            // yield the screen-top slot while WeaponCore's target HUD is showing (opt-in)
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

            // true angular cone. screen-space distance would be an ellipse, not a circle: x and y
            // are both -1..1 across a viewport that is wider than it is tall, so one x unit spans
            // a larger angle than one y unit
            var coneRadians = MathHelper.ToRadians(MathHelper.Clamp(CoopHud.ReticleFov, 1f, 80f));
            var coneCosine = Math.Cos(coneRadians);

            // a box hugging the view frustum, not a sphere around the camera. the sphere returned
            // everything within range in every direction -- 2434 entities in a measured scene --
            // when only what is in front of us can matter, and the loop then walked all of them.
            // the frustum box returns the same survivors from ~20x fewer entities. a grid we are
            // standing inside still comes back: its aabb contains the camera, where the box starts
            var halfHeight = Math.Tan(camera.FovWithZoom * 0.5) * distance;
            var viewport = camera.ViewportSize;
            var obb = new MyOrientedBoundingBoxD(
                camera.Position + camera.WorldMatrix.Forward * (distance * 0.5),
                new Vector3D(halfHeight * (viewport.X / viewport.Y), halfHeight, distance * 0.5),
                Quaternion.CreateFromRotationMatrix(camera.WorldMatrix));

            var result = ListPool<MyEntity>.Instance.Get();
            MyGamePruningStructure.GetAllEntitiesInOBB(ref obb, result, MyEntityQueryType.Both);

            var minBlocks = CoopHud.MinTargetBlocks;

            // what the crosshair is actually on. screen centre is the camera's forward axis
            var aimRay = new RayD(camera.Position, camera.WorldMatrix.Forward);

            var grids = GridSearchPool.Get();
            foreach (var entity in result)
            {
                var grid = entity as IMyCubeGrid;
                if (grid == null) continue;
                if (VRageUtils.IsInAnySafeZone(grid.EntityId)) continue;
                if (grid.Physics == null) continue; // projection

                // debris filter. a split keeps the parent's mod storage, so shattered armour still
                // reports takeover state -- with no control blocks left on it, which reads as an
                // already-neutralized 0/0 target and steals the meter from the ship that shed it.
                // BlocksCount is a HashSet count, so rejecting here is free and skips the Analyze
                // parse below, which the profile puts at ~80% of this update
                var cubeGrid = grid as MyCubeGrid;
                if (minBlocks > 0 && cubeGrid != null && cubeGrid.BlocksCount < minBlocks) continue;

                // crosshair pass: the aim ray against the grid's OWN oriented box. a hit is the
                // authoritative pick -- pointing at a hull is targeting it, at any range or size.
                // everything below measures the grid's PIVOT (block 0,0,0, usually out at a corner
                // of the hull, sometimes off it entirely), whose angle from the aim ray grows as
                // 1/distance: the budget is distance*tan(cone), ~18m at 100m and ~9m at 50m, so a
                // ship filling the screen fails the cone and drops out exactly when you close in.
                // a camera inside the box yields a negative tmin, hence the clamp -- inside ranks
                // first, which also subsumes the old WorldAABB "enclosing" case more accurately
                var obbHit = new MyOrientedBoundingBoxD(grid.LocalAABB, grid.WorldMatrix).Intersects(ref aimRay);
                var aimDistance = obbHit.HasValue ? Math.Max(0, obbHit.Value) : 0;

                var gridPosition = grid.WorldMatrix.Translation;
                var dot = Vector3D.Dot((gridPosition - camera.WorldMatrix.Translation).Normalized(), camera.WorldMatrix.Forward);
                var offAxis = 1 - dot; // 0 dead centre, grows with the angle
                var enclosing = grid.WorldAABB.Contains(characterPosition) != ContainmentType.Disjoint;

                if (!obbHit.HasValue)
                {
                    if (dot <= 0) continue; // behind the camera

                    // nothing off screen can matter -- the boss overlay requires it and the capmeter
                    // cone is a subset of it -- so reject here, before Analyze, which is the one
                    // expensive step in this loop
                    if (!enclosing && !IsOnScreen(gridPosition)) continue;
                }

                var analysis = Analyze(grid);
                if (analysis.Owner == GridOwnerType.Player) continue; // non pvp
                if (analysis.FactionTag == "MERC") continue;

                // boss reticle is its own overlay: drawn for any ork boss within range and on
                // screen, regardless of what the capmeter is aimed at
                if (IsBossOrk(ref analysis) && IsIntactConstruct(grid)) TryAddReticle(ref analysis, gridPosition);

                if (!obbHit.HasValue && dot < coneCosine && !enclosing) continue; // capmeter candidates only

                // ranking bands, lowest first:
                //   [-1000, -990]  crosshair is on the hull, nearest hit first
                //   [-100, -99)    inside the grid's world aabb (legacy enclosing)
                //   [0, 1)         off-axis angle to the pivot
                // the pivot angle is only a fallback now, for grids the crosshair misses. it stays
                // because it is what lets you pick up a ship you are near but not pointed at.
                // note this is a RANKING, not a hit test: an occluded winner still has to clear
                // the line-of-sight walk below, which is what stops targeting through a rock
                var weight = obbHit.HasValue
                    ? -1000 + aimDistance * 0.001
                    : enclosing
                        ? -100 + offAxis
                        : offAxis;

                grids[weight] = analysis;
            }

            ListPool<MyEntity>.Instance.Release(result);

            // walk outwards from the crosshair and take the first one actually in view, so a grid
            // behind an asteroid doesn't get picked through it. each miss costs a raycast, hence
            // the budget: candidates are cone-limited so it is rarely spent, and giving up early
            // just falls back to the best grid by angle
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

            // everything we managed to check was behind cover, so show nothing rather than
            // targeting through it -- falling back to the best by angle here would undo the whole
            // check whenever there is only one candidate. running out of raycasts is different:
            // we cannot claim those were occluded, so that case does fall back
            if (target.Grid == null && ranOutOfChecks) target = nearestToCrosshair;

            GridSearchPool.Release(grids);

            if (target.Grid == null) return false;

            // the capmeter's target gets brackets as well; a boss already has them from the
            // overlay above, so gate it out rather than drawing the same grid twice
            var isBoss = IsBossOrk(ref target);
            if (!isBoss) TryAddReticle(ref target, target.Grid.WorldMatrix.Translation);

            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(target.Grid, out state)) return false;

            var playerGroup = CoopGridTakeover.GetPlayerGroup(player.IdentityId);
            var takeoverReady = state.CanTakeOver && (state.TakeoverPlayerGroup == 0 || state.TakeoverPlayerGroup == playerGroup);

            // count what is STILL hostile rather than subtracting successes from the live length:
            // grinding a controller drops it out of the array entirely, and that is progress in
            // exactly the way flipping its ownership is. subtracting would credit it twice
            var remaining = state.Controllers.Count(id => id != 0 && id != playerGroup);

            // denominator is the high-water mark, not the live length, so removing one of four
            // reads 3/4 instead of renormalizing to 3/3 and swallowing the block you just removed
            var takeoverTargetCount = Math.Max(state.MaxControllers, state.Controllers.Length);

            _state.Title = target.Grid.CustomName;
            _state.Subtitle = isBoss ? "This is the boss Ork! Neutralize it to reclaim the trading hub!" : "";
            _state.SubtitleHighlight = isBoss ? "boss Ork" : null;
            _state.Description = !takeoverReady
                ? "To neutralize a wild grid, take over its remote blocks and control seats."
                : "You can capture a neutralized grid into a garage block.";
            // reads as the grid's remaining hold: starts full and drains as blocks are taken over
            _state.Progress = takeoverTargetCount == 0 ? 0 : (double)remaining / takeoverTargetCount;
            _state.ValueText = $"{remaining}/{takeoverTargetCount}";
            _state.HealthStyle = true;
            // the drained bar goes green but says nothing; the grid is neutralized and the next
            // move is the garage block, which the description only spells out in detailed mode
            _state.CompleteText = takeoverReady ? "READY TO CAPTURE" : null;

            return true;
        }

        /// <summary>Queues brackets for a grid, if it is on screen, far enough away and there is room.</summary>
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
                IsBoss = analysis.SpawnGroupIndex == 0 && analysis.FactionTag == "PORKS",
            });
        }

        /// <summary>
        ///     The one boss grid: spawn-group 0 of the ork faction. Escorts share the faction but
        ///     carry a higher index, and a grid whose MES context could not be read has -1.
        /// </summary>
        static bool IsBossOrk(ref Analysis analysis)
        {
            return analysis.SpawnGroupIndex == 0 && analysis.FactionTag == "PORKS";
        }

        static bool IsOnScreen(Vector3D position)
        {
            var screen = MyAPIGateway.Session.Camera.WorldToScreen(ref position);

            return Math.Abs(screen.X) <= 1 && Math.Abs(screen.Y) <= 1;
        }

        /// <summary>
        ///     Whether the crosshair can reach the grid without terrain in the way. Only voxels
        ///     count as cover: another ship between you and the target is usually the target's own
        ///     escort or scaffolding, and vetoing on those would flip the meter around constantly.
        ///     Costs one raycast per candidate, but the caller stops at the first grid in view and
        ///     only grids inside the cone get this far.
        /// </summary>
        static bool HasLineOfSight(IMyCubeGrid grid, Vector3D from, Vector3D characterPosition)
        {
            // standing inside it: the ray would start within the hull and report nothing useful
            if (grid.WorldAABB.Contains(characterPosition) != ContainmentType.Disjoint) return true;

            // CastRay, not CastLongRay: the long variant does not collide with voxels at all, so
            // it reports the grid through the asteroid in front of it. CastRay is ~4x dearer and
            // is cluster-bound, but a ray that outranges its cluster just returns no hit, which
            // reads as "visible" -- the same answer we gave before this check existed
            IHitInfo hit;
            MyAPIGateway.Physics.CastRay(from, grid.WorldAABB.Center, out hit);

            return !(hit?.HitEntity is IMyVoxelBase);
        }

        /// <summary>
        ///     Whether this is still the spawned boss ship rather than wreckage shed from it. A
        ///     split keeps the grid's mod storage, so armour breaking off the boss carries the
        ///     same MesGridContext and would otherwise get its own brackets; only the piece that
        ///     kept the control blocks has takeover state.
        /// </summary>
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
            // 0 is a real spawn-group index (the boss), so it can't double as "no context read":
            // every ork escort would come back as a boss
            analysis.SpawnGroupIndex = -1;

            var ownerId = grid.BigOwners.GetElementAtOrDefault(0, 0);
            analysis.Owner = VRageUtils.GetOwnerType(ownerId);
            analysis.FactionTag = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId)?.Tag;

            analysis.SpawnGroupIndex = GetSpawnGroupIndex(grid);

            return analysis;
        }

        /// <summary>
        ///     Spawn-group index, cached. Reading it costs a base64 decode, a protobuf
        ///     deserialize and an xml parse -- measured at ~80% of this whole update -- to get a
        ///     number that MES writes once and never changes. The cache is keyed on the raw blob
        ///     rather than the grid alone, so a grid analysed in the window before MES has written
        ///     its context re-reads instead of caching the miss forever.
        /// </summary>
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

        /// <summary>Drops entries for grids that are gone; clears outright if that wasn't enough.</summary>
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
