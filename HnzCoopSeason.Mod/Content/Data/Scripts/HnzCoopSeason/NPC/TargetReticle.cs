using System.Collections.Generic;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.NPC
{
    /// <summary>Screen-space target marker: four corner brackets and a centre dot, one instance per target.</summary>
    public sealed class TargetReticle : HudElementBase
    {
        public const int PoolSize = 16;

        const float BoxHalf = 20;

        // screen halves; the box is allowed past the viewport, this only keeps the arithmetic finite
        const float MaxScreenOverflow = 4;
        const float ArmLength = 9;
        const float Thickness = 2;
        const float DotSize = 2;

        static readonly Color ReticleColor = new Color(187, 233, 246, 220);
        static readonly Color BossColor = new Color(237, 0, 211, 220);

        static readonly List<Target> Targets = new List<Target>(PoolSize);

        // matches NpcHud's 5-frame target-refresh cadence
        const int SizeRefreshInterval = 5;

        readonly int _slot;
        readonly TexturedBox[] _strips;
        readonly TexturedBox _dot;
        bool? _appliedBoss;
        long _sizedForEntity; // 0 = nothing cached

        public TargetReticle(HudParentBase parent, int slot) : base(parent)
        {
            _slot = slot;
            Size = Vector2.Zero;

            _strips = new TexturedBox[8];
            for (var i = 0; i < _strips.Length; i++)
            {
                _strips[i] = new TexturedBox(this) { Color = ReticleColor };
            }

            _dot = new TexturedBox(this)
            {
                Color = ReticleColor,
                Size = new Vector2(DotSize),
            };

            LayoutBrackets(BoxHalf, BoxHalf);
        }

        public struct Target
        {
            public long EntityId; // slot reuse invalidates the cached size
            public IMyCubeGrid Grid;
            public Vector3D Position;
            public BoundingBoxD LocalBox;
            public MatrixD WorldMatrix;
            public bool IsBoss;
        }

        public static void RefreshTransforms()
        {
            for (var i = Targets.Count - 1; i >= 0; i--)
            {
                var target = Targets[i];
                var grid = target.Grid;

                if (grid == null || grid.Closed || grid.MarkedForClose)
                {
                    Targets.RemoveAt(i);
                    continue;
                }

                // LocalBox only changes when blocks do, which the 5-frame scan picks up
                target.Position = grid.WorldAABB.Center;
                target.WorldMatrix = grid.WorldMatrix;
                Targets[i] = target; // struct, so it has to be written back
            }
        }

        public static void SetTargets(List<Target> targets)
        {
            Targets.Clear();
            if (targets == null) return;

            for (var i = 0; i < targets.Count && i < PoolSize; i++)
            {
                Targets.Add(targets[i]);
            }
        }

        public static void Clear()
        {
            Targets.Clear();
        }

        void LayoutBrackets(float halfX, float halfY)
        {
            var armX = MathHelper.Min(ArmLength, halfX);
            var armY = MathHelper.Min(ArmLength, halfY);

            for (var i = 0; i < 4; i++)
            {
                var sx = i % 2 == 0 ? -1 : 1;
                var sy = i < 2 ? 1 : -1;

                _strips[i * 2].Size = new Vector2(armX, Thickness);
                _strips[i * 2].Offset = new Vector2(sx * (halfX - armX / 2), sy * (halfY - Thickness / 2));

                _strips[i * 2 + 1].Size = new Vector2(Thickness, armY);
                _strips[i * 2 + 1].Offset = new Vector2(sx * (halfX - Thickness / 2), sy * (halfY - armY / 2));
            }
        }

        /// <summary>Half-extents enclosing the target's 8 projected box corners, never below the default box.</summary>
        static Vector2 ProjectedHalfExtents(ref Target target, Vector3D camPos, Vector3D camForward)
        {
            var fallback = new Vector2(BoxHalf);
            var camera = MyAPIGateway.Session.Camera;
            var min = new Vector2(float.MaxValue);
            var max = new Vector2(float.MinValue);

            var lo = target.LocalBox.Min;
            var hi = target.LocalBox.Max;
            var front = 0;
            var behind = 0;

            for (var i = 0; i < 8; i++)
            {
                // walk the 8 corners via the bit pattern of i
                var local = new Vector3D(
                    (i & 1) == 0 ? lo.X : hi.X,
                    (i & 2) == 0 ? lo.Y : hi.Y,
                    (i & 4) == 0 ? lo.Z : hi.Z);
                var world = Vector3D.Transform(local, target.WorldMatrix);

                // a corner behind the camera projects to nonsense; skip it, not the whole box
                if (Vector3D.Dot(world - camPos, camForward) <= 0)
                {
                    behind++;
                    continue;
                }

                front++;
                var s = camera.WorldToScreen(ref world);

                // a corner grazing the camera plane projects enormous, and the box only ever grows
                var sx = MathHelper.Clamp((float)s.X, -MaxScreenOverflow, MaxScreenOverflow);
                var sy = MathHelper.Clamp((float)s.Y, -MaxScreenOverflow, MaxScreenOverflow);
                min = Vector2.Min(min, new Vector2(sx, sy));
                max = Vector2.Max(max, new Vector2(sx, sy));
            }

            if (front == 0) return fallback;

            // partly behind means the visible corners understate the box, so open it past the viewport
            if (behind > 0)
            {
                min = Vector2.Min(min, new Vector2(-MaxScreenOverflow));
                max = Vector2.Max(max, new Vector2(MaxScreenOverflow));
            }

            var halfWidth = HudMain.ScreenWidth / HudMain.ResScale * 0.5f;
            var halfHeight = HudMain.ScreenHeight / HudMain.ResScale * 0.5f;
            return new Vector2(
                MathHelper.Max(BoxHalf, (max.X - min.X) * 0.5f * halfWidth),
                MathHelper.Max(BoxHalf, (max.Y - min.Y) * 0.5f * halfHeight));
        }

        protected override void Layout()
        {
            var camera = MyAPIGateway.Session.Camera;
            var config = MyAPIGateway.Session.Config;
            var visible = _slot < Targets.Count
                          && camera != null
                          && !config.MinimalHud
                          && !MyAPIGateway.Gui.IsCursorVisible;

            if (visible)
            {
                // brackets around a hull you are standing inside would just frame the whole screen
                var boxed = Targets[_slot];
                var camPosition = camera.Position;
                visible = !new MyOrientedBoundingBoxD(boxed.LocalBox, boxed.WorldMatrix).Contains(ref camPosition);
            }

            foreach (var strip in _strips)
            {
                strip.Visible = visible;
            }

            if (!visible)
            {
                _dot.Visible = false;
                _sizedForEntity = 0;
                return;
            }

            var target = Targets[_slot];
            _dot.Visible = !target.IsBoss;

            // recolour only on change; Layout runs every frame
            if (_appliedBoss != target.IsBoss)
            {
                _appliedBoss = target.IsBoss;
                var color = target.IsBoss ? BossColor : ReticleColor;
                foreach (var strip in _strips)
                {
                    strip.Color = color;
                }

                _dot.Color = color;
            }

            // staggered by slot; Size must grow with the brackets or children get culled
            var frame = MyAPIGateway.Session.GameplayFrameCounter;
            if (_sizedForEntity != target.EntityId || (frame + _slot) % SizeRefreshInterval == 0)
            {
                _sizedForEntity = target.EntityId;

                var half = ProjectedHalfExtents(ref target, camera.Position, camera.WorldMatrix.Forward);
                Size = new Vector2(half.X * 2 + Thickness, half.Y * 2 + Thickness);
                LayoutBrackets(half.X, half.Y);
            }

            var worldPosition = target.Position;
            var screen = camera.WorldToScreen(ref worldPosition);
            var halfWidth = HudMain.ScreenWidth / HudMain.ResScale * 0.5f;
            var halfHeight = HudMain.ScreenHeight / HudMain.ResScale * 0.5f;
            Offset = new Vector2(
                (float)MathHelperD.Clamp(screen.X, -1, 1) * halfWidth,
                (float)MathHelperD.Clamp(screen.Y, -1, 1) * halfHeight);
        }
    }
}
