using System;
using System.Text;
using HudAPI;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace HnzCoopSeason
{
    public sealed class NpcCaptureReticle
    {
        HudAPIv2.HUDMessage _body;

        static HudAPIv2.HUDMessage Create()
        {
            return new HudAPIv2.HUDMessage(
                /*text*/ new StringBuilder("<color=255,0,0>\u00a4"),
                /*origin*/ new Vector2D(0f, 0f),
                /*offset*/ new Vector2D(0f, 0f),
                /*time to live*/ -1,
                /*scale*/ 3,
                /*hide hud*/ true,
                /*shadowing*/ false,
                /*shadow color*/ null,
                /*text*/ MyBillboard.BlendTypeEnum.PostPP);
        }

        public void Close()
        {
            try
            {
                _body?.DeleteMessage();
                _body = null;
            }
            catch (Exception)
            {
                MyLog.Default.Warning("[HnzCoopSeason] failed to delete npc capture reticle");
            }
        }

        public void Update(Vector3D targetPosition, bool active, double minDistance)
        {
            var camera = MyAPIGateway.Session.Camera;
            if (camera == null) return;

            if (!active || Vector3D.Distance(camera.WorldMatrix.Translation, targetPosition) < minDistance)
            {
                _body?.DeleteMessage();
                _body = null;
                return;
            }

            if (_body == null)
            {
                _body = Create();
            }

            var screenPosition = WorldToScreen(camera, targetPosition);
            screenPosition += _body.GetTextLength() / 2 * -1;

            _body.Offset = new Vector2D(
                MathHelperD.Clamp(screenPosition.X, -1, 1),
                MathHelperD.Clamp(screenPosition.Y, -1, 1));
        }

        static Vector2D WorldToScreen(IMyCamera camera, Vector3D worldPosition)
        {
            var screenPosition = camera.WorldToScreen(ref worldPosition);
            return new Vector2D(screenPosition.X, screenPosition.Y);
        }
    }
}