using System.Collections.Generic;
using System.Linq;
using System.Text;
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

        HudElementStack _group;
        HudElement _titleElement;
        HudElement _subtitleElement;
        HudElement _progressElement;
        HudElement _descriptionElement;
        NpcHudReticle _reticle;
        Vector3D? _reticlePosition;

        public void Load()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            _group = new HudElementStack
            {
                Padding = -0.02,
                Offset = -0.1,
            };

            ScreenTopHud.Instance.AddGroup(nameof(NpcHud), _group, 1);

            _progressElement = new HudElement().AddTo(_group);
            _titleElement = new HudElement().AddTo(_group);
            _subtitleElement = new HudElement().AddTo(_group);
            _descriptionElement = new HudElement().AddTo(_group);
            _reticle = new NpcHudReticle();
        }

        public void Unload()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            _titleElement.Clear();
            _subtitleElement.Clear();
            _progressElement.Clear();
            _descriptionElement.Clear();
            _group.Clear();
            _reticle.ClearBody();
            ScreenTopHud.Instance.RemoveGroup(nameof(NpcHud));
        }

        public void Update()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            _reticle.Update(_reticlePosition ?? Vector3D.Zero, _reticlePosition.HasValue, 1000);

            if (MyAPIGateway.Session.GameplayFrameCounter % 5 != 0) return;

            _reticlePosition = null;
            var canRender = TryApplyHudElements();
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

            var titleText = $"<color=0,255,255>{target.Grid.CustomName}";
            var subtitleText = target.SpawnGroupIndex == 0 && target.FactionTag == "PORKS"
                ? "<color=0,255,255>This is the boss Ork! Neutralize it to reclaim the trading hub!"
                : "";

            var descriptionText = !takeoverReady
                ? "To neutralize a wild grid, take over all their remote blocks and control seats."
                : "You can capture a neutralized grid into a garage block.";

            _progressElement.Apply(CreateProgressionBar(takeoverSuccessCount, takeoverTargetCount));
            _titleElement.Apply(titleText, 1.2);
            _subtitleElement.Apply(subtitleText);
            _descriptionElement.Apply(descriptionText);

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

        static string CreateProgressionBar(int takeoverCount, int totalCount)
        {
            var buffer = new StringBuilder();
            buffer.Append("CAPMETER ");

            var progress = totalCount == 0 ? 1 : (float)takeoverCount / totalCount;
            buffer.Append(HudElement.CreateProgressionBar(progress));

            buffer.Append($" {takeoverCount}/{totalCount}");

            return buffer.ToString();
        }
    }
}