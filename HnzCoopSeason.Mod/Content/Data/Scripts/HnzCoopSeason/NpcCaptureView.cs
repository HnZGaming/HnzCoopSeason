﻿using System;
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
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class NpcCaptureView
    {
        public static readonly NpcCaptureView Instance = new NpcCaptureView();

        static readonly Pool<SortedList<double, CoopGrids.Analysis>> GridSearchPool =
            new Pool<SortedList<double, CoopGrids.Analysis>>(
                () => new SortedList<double, CoopGrids.Analysis>(),
                l => l.Clear());

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

                var gridPosition = grid.WorldMatrix.Translation;
                var screenPosition = camera.WorldToScreen(ref gridPosition);
                var dot = Vector3D.Dot((gridPosition - camera.WorldMatrix.Translation).Normalized(), camera.WorldMatrix.Forward);
                var screenDistance = dot < 0 ? 2 : Vector3D.Distance(screenPosition, new Vector3(0, 0, screenPosition.Z));
                var enclosing = grid.WorldAABB.Contains(characterPosition) != ContainmentType.Disjoint;
                if (screenDistance > 0.3 && !enclosing) continue;

                var analysis = CoopGrids.Analyze(grid);
                if (analysis.Owner == CoopGrids.Owner.Player) continue; // non pvp

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

            int takeoverSuccessCount;
            int takeoverTargetCount;
            var takeoverComplete = CoopGrids.GetTakeoverProgress(target.Grid, true, out takeoverSuccessCount, out takeoverTargetCount);

            var titleText = $"<color=0,255,255>{target.Grid.CustomName}";
            var subtitleText = target.IsOrksLeader
                ? "<color=0,255,255>This is the boss Ork! Neutralize it to reclaim the trading hub!"
                : "";

            var descriptionText = !takeoverComplete
                ? "To neutralize a wild grid, take over all their remote blocks and control seats."
                : "You can capture a neutralized grid into a garage block.";

            _progressElement.Apply(CreateProgressionBar(takeoverSuccessCount, takeoverTargetCount));
            _titleElement.Apply(titleText, 1.2);
            _subtitleElement.Apply(subtitleText);
            _descriptionElement.Apply(descriptionText);

            return true;
        }

        static Vector3D GetReticlePosition(IMyCubeGrid grid)
        {
            return grid.WorldAABB.Center;
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
    }
}