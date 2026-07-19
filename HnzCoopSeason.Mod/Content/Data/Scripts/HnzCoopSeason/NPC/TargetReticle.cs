using System.Collections.Generic;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.NPC
{
    /// <summary>
    ///     Screen-space target marker in the mod's HUD design language: four corner brackets and a
    ///     centre dot, sized to the target's projected box. One instance draws one target; CoopHud
    ///     creates a fixed pool and NpcHud refills the shared target list each update, so every
    ///     visible ork gets brackets rather than only the one being aimed at.
    /// </summary>
    public sealed class TargetReticle : HudElementBase
    {
        public const int PoolSize = 16;

        const float BoxHalf = 20; // half-size of the bracket box, logical px (40x40 minimum)

        // how far past the viewport the bracket box may grow, in screen halves (1 = screen edge).
        // the box is ALLOWED to overflow: a target bigger than the screen should put its corners
        // off screen and simply stop drawing, which is the honest result. capping at the viewport
        // instead pins all four brackets to the screen edges and paints a full-screen frame around
        // nothing. this bound exists purely to keep the arithmetic finite -- a corner grazing the
        // camera plane divides by a near-zero w and projects past 4000 -- not to fit the screen
        const float MaxScreenOverflow = 4;
        const float ArmLength = 9;
        const float Thickness = 2;
        const float DotSize = 2;

        static readonly Color ReticleColor = new Color(187, 233, 246, 220); // vanilla accent
        static readonly Color BossColor = new Color(237, 0, 211, 220); // #ED00D3

        /// <summary>Targets to draw this frame. NpcHud rewrites it; instances read their slot.</summary>
        static readonly List<Target> Targets = new List<Target>(PoolSize);

        // frames between bracket-size recomputes. matches NpcHud's target-refresh cadence: the
        // world data the projection reads is rewritten every 5th frame, so a faster size refresh
        // is only tracking camera motion against an already-stale position. 1 disables the cache.
        const int SizeRefreshInterval = 5;

        readonly int _slot;
        readonly TexturedBox[] _strips; // 8 bracket arms
        readonly TexturedBox _dot;
        bool? _appliedBoss; // last colour pushed to the strips
        long _sizedForEntity; // grid the cached bracket size was computed for; 0 = nothing cached

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
            public long EntityId; // identity, so a slot reused by a different grid invalidates the cached size
            public IMyCubeGrid Grid; // kept so the transform can be re-read without another scan
            public Vector3D Position;
            public BoundingBoxD LocalBox;
            public MatrixD WorldMatrix;
            public bool IsBoss;
        }

        /// <summary>
        ///     Re-reads each target's live transform. NpcHud only re-runs its selection every 5th
        ///     frame — which grids qualify changes slowly — but the grids themselves keep moving,
        ///     so brackets built from a 12Hz snapshot trail a mover by up to 83ms (~8m at 100 m/s).
        ///     Measured: 0.0007ms for a full pool of 16, against 0.018ms to re-run the whole scan.
        ///     Cheaper than the scan's own amortized cost, so this is close to free accuracy.
        ///     Targets whose grid has gone are dropped rather than left to linger to the next scan.
        /// </summary>
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

                // position and orientation only: LocalBox changes solely when blocks are added or
                // removed, which the 5-frame scan picks up soon enough
                target.Position = grid.WorldAABB.Center;
                target.WorldMatrix = grid.WorldMatrix;
                Targets[i] = target; // struct, so it has to be written back
            }
        }

        /// <summary>Called by NpcHud every update; safe before the elements exist.</summary>
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
            // corner Ls: horizontal arm + vertical arm per corner
            var armX = MathHelper.Min(ArmLength, halfX);
            var armY = MathHelper.Min(ArmLength, halfY);

            for (var i = 0; i < 4; i++)
            {
                var sx = i % 2 == 0 ? -1 : 1; // left/right
                var sy = i < 2 ? 1 : -1; // top/bottom

                _strips[i * 2].Size = new Vector2(armX, Thickness);
                _strips[i * 2].Offset = new Vector2(sx * (halfX - armX / 2), sy * (halfY - Thickness / 2));

                _strips[i * 2 + 1].Size = new Vector2(Thickness, armY);
                _strips[i * 2 + 1].Offset = new Vector2(sx * (halfX - Thickness / 2), sy * (halfY - armY / 2));
            }
        }

        /// <summary>
        ///     Projects the target's 8 oriented-box corners and returns the half-extents that
        ///     enclose them, never smaller than the default bracket box.
        /// </summary>
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

                // a corner behind the camera projects to nonsense, so it is skipped rather than
                // abandoning the whole box -- otherwise flying up to a big ship, where the rear
                // corners fall behind you, would snap the brackets back to minimum size
                if (Vector3D.Dot(world - camPos, camForward) <= 0)
                {
                    behind++;
                    continue;
                }

                front++;
                var s = camera.WorldToScreen(ref world);

                // bound before accumulating, but well outside the viewport -- see MaxScreenOverflow.
                // a corner grazing the camera plane divides by a near-zero w and projects enormous
                // (measured: a point 1mm ahead and 5m to the side lands at x=4473), and the widening
                // below only ever expands, so one such corner would blow the box up without bound
                var sx = MathHelper.Clamp((float)s.X, -MaxScreenOverflow, MaxScreenOverflow);
                var sy = MathHelper.Clamp((float)s.Y, -MaxScreenOverflow, MaxScreenOverflow);
                min = Vector2.Min(min, new Vector2(sx, sy));
                max = Vector2.Max(max, new Vector2(sx, sy));
            }

            if (front == 0) return fallback; // entirely behind the camera

            // partly behind means the target wraps past the camera, so the visible corners badly
            // understate it -- the true extent on the clipped axes is unbounded. open the box past
            // the viewport rather than to it: something extending behind you is filling the view,
            // and brackets drawn exactly on the screen edges would frame the whole screen
            if (behind > 0)
            {
                min = Vector2.Min(min, new Vector2(-MaxScreenOverflow));
                max = Vector2.Max(max, new Vector2(MaxScreenOverflow));
            }

            // normalized screen (-1..1) -> logical px half-extents
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
                // brackets around a hull you are standing inside just frame the whole screen and
                // say nothing: half the corners fall behind the camera, so they are skipped, and
                // the widening in ProjectedHalfExtents then opens the box out to the viewport.
                // measured from inside an ork grid: 4/8 corners behind, box 1290x702 against a
                // 1290x540 screen half -- full width and 162px past both edges. drop it instead
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
                _sizedForEntity = 0; // recompute the moment it comes back, don't flash a stale box
                return;
            }

            var target = Targets[_slot];
            _dot.Visible = !target.IsBoss; // the boss brackets read cleaner without a centre dot

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

            // SIZE -- the 8-corner projection, on the refresh cadence rather than every frame.
            // apparent size tracks distance, which changes slowly, and the world box being
            // projected is itself up to 5 frames old. staggered by slot so the pool never
            // recomputes in lockstep. a slot reused by a different grid rebuilds immediately.
            // grow Size with the brackets: children outside the parent's bounds get culled
            var frame = MyAPIGateway.Session.GameplayFrameCounter;
            if (_sizedForEntity != target.EntityId || (frame + _slot) % SizeRefreshInterval == 0)
            {
                _sizedForEntity = target.EntityId;

                var half = ProjectedHalfExtents(ref target, camera.Position, camera.WorldMatrix.Forward);
                Size = new Vector2(half.X * 2 + Thickness, half.Y * 2 + Thickness);
                LayoutBrackets(half.X, half.Y);
            }

            // POSITION -- every frame, unconditionally. camera rotation dominates where the target
            // lands on screen, so anything slower here and the brackets slide off it as you turn
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
