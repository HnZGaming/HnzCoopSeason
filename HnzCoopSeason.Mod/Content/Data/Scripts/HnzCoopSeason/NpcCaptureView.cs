using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HnzCoopSeason.Utils;
using HnzCoopSeason.Utils.Hud;
using HnzCoopSeason.Utils.Pools;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class NpcCaptureView
    {
        public static readonly NpcCaptureView Instance = new NpcCaptureView();

        HudElementStack _group;
        HudElement _titleElement;
        HudElement _subtitleElement;
        HudElement _progressElement;
        HudElement _descriptionElement;
        NpcCaptureReticle _reticle;
        Vector3D? _reticlePosition;

        public void Load()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return; // client

            _group = new HudElementStack
            {
                Padding = -0.02,
                Offset = -0.1,
            };

            ScreenTopView.Instance.AddGroup(nameof(NpcCaptureView), _group, 1);

            _progressElement = new HudElement().AddTo(_group);
            _titleElement = new HudElement().AddTo(_group);
            _subtitleElement = new HudElement().AddTo(_group);
            _descriptionElement = new HudElement().AddTo(_group);
            _reticle = new NpcCaptureReticle();
        }

        public void Unload()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return; // client

            _titleElement.Clear();
            _subtitleElement.Clear();
            _progressElement.Clear();
            _descriptionElement.Clear();
            _group.Clear();
            _reticle.ClearBody();
            ScreenTopView.Instance.RemoveGroup(nameof(NpcCaptureView));
        }

        public void Update()
        {
            if (MyAPIGateway.Utilities.IsDedicated) return; // client

            _reticle.Update(_reticlePosition ?? Vector3D.Zero, _reticlePosition.HasValue, 1000);

            if (MyAPIGateway.Session.GameplayFrameCounter % 5 != 0) return;

            _reticlePosition = null;
            var canRender = TryApplyHudElements();
            ScreenTopView.Instance.SetActive(nameof(NpcCaptureView), canRender);
        }

        bool TryApplyHudElements() // false if deactivating the view
        {
            var character = MyAPIGateway.Session.Player?.Character;
            if (character == null) return false;

            List<GridSearch> grids;
            using (ListPool<GridSearch>.Instance.GetUntilDispose(out grids))
            {
                CollectNearbyGrids(MyAPIGateway.Session.Camera, character, 10 * 1000, grids);
                if (TryApplyHudElements(grids)) return true;
            }

            return false;
        }

        static void CollectNearbyGrids(IMyCamera camera, IMyCharacter character, double distance, ICollection<GridSearch> grids)
        {
            List<MyEntity> result;
            using (ListPool<MyEntity>.Instance.GetUntilDispose(out result))
            {
                var characterPosition = character.WorldMatrix.Translation;
                var sphere = new BoundingSphereD(characterPosition, distance);
                MyGamePruningStructure.GetAllEntitiesInSphere(ref sphere, result, MyEntityQueryType.Both);
                foreach (var entity in result)
                {
                    var grid = entity as IMyCubeGrid;
                    if (grid == null) continue;

                    var gridPosition = grid.WorldMatrix.Translation;
                    var screenPosition = camera.WorldToScreen(ref gridPosition);
                    var dot = Vector3D.Dot((gridPosition - camera.WorldMatrix.Translation).Normalized(), camera.WorldMatrix.Forward);
                    var screenDistance = dot < 0 ? 2 : Vector3D.Distance(screenPosition, new Vector3(0, 0, screenPosition.Z));
                    var enclosing = grid.WorldAABB.Contains(characterPosition) != ContainmentType.Disjoint;
                    var analysis = CoopGrids.Analyze(grid);
                    grids.Add(new GridSearch(analysis, screenDistance, enclosing));
                }
            }
        }

        bool TryApplyHudElements(List<GridSearch> grids)
        {
            var target = grids
                .Where(g => FilterGrid(g))
                .OrderBy(g => OrderGrid(g))
                .FirstOrDefault();

            _reticlePosition = target?.Analysis.Grid.WorldMatrix.Translation;

            if (target == null) return false;

            int takeoverSuccessCount;
            int takeoverTargetCount;
            var takeoverComplete = CoopGrids.GetTakeoverProgress(target.Analysis.Grid, true, out takeoverSuccessCount, out takeoverTargetCount);

            var titleText = target.Analysis.Grid.CustomName;
            var subtitleText = target.Analysis.IsOrksLeader
                ? "<color=0,255,255>This is the boss Ork! Neutralize it to reclaim the trading hub!"
                : "";

            var descriptionText = !takeoverComplete
                ? "To neutralize a wild grid, take over all their remote blocks and control seats."
                : "You can capture a neutralized grid into a garage block.";

            _progressElement.Apply(CreateProgressionBar(takeoverSuccessCount, takeoverTargetCount));
            _titleElement.Apply(titleText, 1.5);
            _subtitleElement.Apply(subtitleText);
            _descriptionElement.Apply(descriptionText);

            return true;
        }

        static bool FilterGrid(GridSearch grid)
        {
            if (grid.Analysis.Owner == CoopGrids.Owner.Player) return false;
            if (VRageUtils.IsInAnySafeZone(grid.Analysis.Grid.EntityId)) return false;
            if (grid.ScreenDistance > 0.4 && !grid.Enclosing && !grid.Analysis.IsOrksLeader) return false;
            return true;
        }

        static double OrderGrid(GridSearch grid)
        {
            var value = 0d;
            value += grid.Analysis.IsOrksLeader ? -1000 : 0;
            value += grid.Enclosing ? -100 : 0;
            value += grid.ScreenDistance;
            return value;
        }

        static string CreateProgressionBar(int takeoverCount, int totalCount)
        {
            var buffer = new StringBuilder();
            buffer.Append("CAPMETER ");

            var progress = (float)takeoverCount / totalCount;
            buffer.Append(HudElement.CreateProgressionBar(progress));

            buffer.Append($" {takeoverCount}/{totalCount}");

            return buffer.ToString();
        }

        sealed class GridSearch
        {
            public readonly CoopGrids.Analysis Analysis;
            public readonly double ScreenDistance;
            public readonly bool Enclosing;

            public GridSearch(CoopGrids.Analysis analysis, double screenDistance, bool enclosing)
            {
                Analysis = analysis;
                ScreenDistance = screenDistance;
                Enclosing = enclosing;
            }
        }
    }
}