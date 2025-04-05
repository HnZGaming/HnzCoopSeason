using System;
using System.Text;
using HudAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace HnzCoopSeason.Utils.ScreenSpace
{
    public sealed class Element
    {
        HudAPIv2.HUDMessage _message;

        public bool IsDirty { get; set; }

        public void Clear()
        {
            try
            {
                _message.DeleteMessage();
                _message = null;
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed deleting message; {e}");
            }
        }

        public Element AddedTo(ElementGroup group)
        {
            group.Add(this);
            return this;
        }

        public void SetText(string text, double scale = 1d)
        {
            _message?.DeleteMessage();
            _message = new HudAPIv2.HUDMessage(
                /*text*/ new StringBuilder(text),
                /*origin*/ new Vector2D(0f, 1f),
                /*offset*/ new Vector2D(0f, 0f),
                /*time to live*/ -1,
                /*scale*/ scale,
                /*hide hud*/ true,
                /*shadowing*/ false,
                /*shadow color*/ null,
                /*text*/ MyBillboard.BlendTypeEnum.PostPP);

            IsDirty = true;
        }

        public double Render(double offset)
        {
            if (_message == null) return 0;

            var textLength = _message.GetTextLength();
            _message.Offset = new Vector2D(-textLength.X / 2, offset);

            return textLength.Y;
        }
    }
}