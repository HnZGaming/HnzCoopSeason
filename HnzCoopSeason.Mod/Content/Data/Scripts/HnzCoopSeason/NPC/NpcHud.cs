using System.Collections.Generic;
using System.Linq;
using GridStorage.API;
using HnzCoopSeason.HudUtils;
using HnzCoopSeason.Spawners;
using HnzUtils;
using HnzUtils.Pools;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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

        MeterState _state;
        Vector3D? _reticlePosition;

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

            TargetReticle.Set(Vector3D.Zero, false, 0);
            ScreenTopHud.Instance.RemoveGroup(nameof(NpcHud));
            _state = null;
        }

        public void Update()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            TargetReticle.Set(_reticlePosition ?? Vector3D.Zero, _reticlePosition.HasValue && CoopHud.ShowTargetReticle, 1000);

            if (MyAPIGateway.Session.GameplayFrameCounter % 5 != 0) return;

            _reticlePosition = null;
            // yield the screen-top slot while WeaponCore's target HUD is showing (opt-in)
            var canRender = !CoopHud.YieldToWeaponCore && TryApplyHudElements();
            ScreenTopHud.Instance.SetActive(nameof(NpcHud), canRender);
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
            var sphere = new BoundingSphereD(characterPosition, distance);

            var result = ListPool<MyEntity>.Instance.Get();
            MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, result, MyEntityQueryType.Both);

            IHitInfo raycastHitInfo;
            MyAPIGateway.Physics.CastLongRay(camera.Position, camera.Position + camera.WorldMatrix.Forward * distance, out raycastHitInfo, true);

            var grids = GridSearchPool.Get();
            foreach (var entity in result)
            {
                var grid = entity as IMyCubeGrid;
                if (grid == null) continue;
                if (VRageUtils.IsInAnySafeZone(grid.EntityId)) continue;
                if (grid.Physics == null) continue; // projection

                var gridPosition = grid.WorldMatrix.Translation;
                var screenPosition = camera.WorldToScreen(ref gridPosition);
                var dot = Vector3D.Dot((gridPosition - camera.WorldMatrix.Translation).Normalized(), camera.WorldMatrix.Forward);
                var screenDistance = dot < 0 ? 2 : Vector3D.Distance(screenPosition, new Vector3(0, 0, screenPosition.Z));
                var enclosing = grid.WorldAABB.Contains(characterPosition) != ContainmentType.Disjoint;
                if (screenDistance > 0.3 && !enclosing) continue;

                var analysis = Analyze(grid);
                if (analysis.Owner == GridOwnerType.Player) continue; // non pvp
                if (analysis.FactionTag == "MERC") continue;

                var raycastHit = raycastHitInfo?.HitEntity == grid;

                var weight = 0d;
                weight += raycastHit ? -1000 : 0;
                weight += enclosing ? -100 : 0;
                weight += screenDistance;

                grids[weight] = analysis;
            }

            ListPool<MyEntity>.Instance.Release(result);

            var target = grids.FirstOrDefault().Value;
            GridSearchPool.Release(grids);

            _reticlePosition = target.Grid == null ? (Vector3D?)null : GetReticlePosition(target.Grid);

            if (target.Grid == null) return false;

            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(target.Grid, out state)) return false;

            var playerGroup = CoopGridTakeover.GetPlayerGroup(player.IdentityId);
            var takeoverReady = state.CanTakeOver && (state.TakeoverPlayerGroup == 0 || state.TakeoverPlayerGroup == playerGroup);
            var takeoverTargetCount = state.Controllers.Length;
            var takeoverSuccessCount = state.Controllers.Count(id => id == 0 || id == playerGroup);

            _state.Title = target.Grid.CustomName;
            var isBoss = target.SpawnGroupIndex == 0 && target.FactionTag == "PORKS";
            _state.Subtitle = isBoss ? "This is the boss Ork! Neutralize it to reclaim the trading hub!" : "";
            _state.SubtitleHighlight = isBoss ? "boss Ork" : null;
            _state.Description = !takeoverReady
                ? "To neutralize a wild grid, take over its remote blocks and control seats."
                : "You can capture a neutralized grid into a garage block.";
            // reads as the grid's remaining hold: starts full and drains as blocks are taken over
            var remaining = takeoverTargetCount - takeoverSuccessCount;
            _state.Progress = takeoverTargetCount == 0 ? 0 : (double)remaining / takeoverTargetCount;
            _state.ValueText = $"{remaining}/{takeoverTargetCount}";
            _state.HealthStyle = true;

            return true;
        }

        static Analysis Analyze(IMyCubeGrid grid)
        {
            var analysis = default(Analysis);
            analysis.Grid = grid;

            var ownerId = grid.BigOwners.GetElementAtOrDefault(0, 0);
            analysis.Owner = VRageUtils.GetOwnerType(ownerId);
            analysis.FactionTag = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId)?.Tag;

            MesGridContext context;
            if (MesGridGroup.TryGetSpawnContext(grid, out context))
            {
                analysis.SpawnGroupIndex = context.Index;
            }

            return analysis;
        }

        static Vector3D GetReticlePosition(IMyCubeGrid grid)
        {
            return grid.WorldAABB.Center;
        }
    }
}
