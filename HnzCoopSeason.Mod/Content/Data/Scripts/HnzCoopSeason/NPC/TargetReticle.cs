using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRageMath;

namespace HnzCoopSeason.NPC
{
    /// <summary>
    ///     Screen-space target marker in the mod's HUD design language:
    ///     four corner brackets + center dot in the vanilla accent cyan.
    ///     NpcHud writes the target state; this element projects and draws it.
    ///     Created by CoopHud (requires RichHud); safe to Set() before that.
    /// </summary>
    public sealed class TargetReticle : HudElementBase
    {
        const float BoxHalf = 17; // half-size of the bracket box, logical px
        const float ArmLength = 9;
        const float Thickness = 2;
        const float DotSize = 2;

        static readonly Color ReticleColor = new Color(187, 233, 246, 220); // vanilla accent

        static Vector3D _targetPosition;
        static bool _active;
        static double _minDistance;

        readonly TexturedBox[] _strips; // 8 bracket arms
        readonly TexturedBox _dot;

        public TargetReticle(HudParentBase parent) : base(parent)
        {
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

            LayoutBrackets();
        }

        /// <summary>Called by NpcHud every frame (works even before the element exists).</summary>
        public static void Set(Vector3D targetPosition, bool active, double minDistance)
        {
            _targetPosition = targetPosition;
            _active = active;
            _minDistance = minDistance;
        }

        void LayoutBrackets()
        {
            // corner Ls: horizontal arm + vertical arm per corner
            for (var i = 0; i < 4; i++)
            {
                var sx = i % 2 == 0 ? -1 : 1; // left/right
                var sy = i < 2 ? 1 : -1; // top/bottom

                _strips[i * 2].Size = new Vector2(ArmLength, Thickness);
                _strips[i * 2].Offset = new Vector2(sx * (BoxHalf - ArmLength / 2), sy * (BoxHalf - Thickness / 2));

                _strips[i * 2 + 1].Size = new Vector2(Thickness, ArmLength);
                _strips[i * 2 + 1].Offset = new Vector2(sx * (BoxHalf - Thickness / 2), sy * (BoxHalf - ArmLength / 2));
            }
        }

        protected override void Layout()
        {
            var camera = MyAPIGateway.Session.Camera;
            var config = MyAPIGateway.Session.Config;
            var visible = _active
                          && camera != null
                          && !config.MinimalHud
                          && !MyAPIGateway.Gui.IsCursorVisible
                          && Vector3D.Dot(_targetPosition - camera.Position, camera.WorldMatrix.Forward) > 0
                          && Vector3D.Distance(camera.Position, _targetPosition) >= _minDistance;

            foreach (var strip in _strips)
            {
                strip.Visible = visible;
            }

            _dot.Visible = visible;
            if (!visible) return;

            // project to screen; RichHud root offsets are logical px from screen center
            var worldPosition = _targetPosition;
            var screen = camera.WorldToScreen(ref worldPosition);
            var halfWidth = HudMain.ScreenWidth / HudMain.ResScale * 0.5f;
            var halfHeight = HudMain.ScreenHeight / HudMain.ResScale * 0.5f;
            Offset = new Vector2(
                (float)MathHelperD.Clamp(screen.X, -1, 1) * halfWidth,
                (float)MathHelperD.Clamp(screen.Y, -1, 1) * halfHeight);
        }
    }
}
