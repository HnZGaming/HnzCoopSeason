using System;
using System.Text;
using HudAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace HnzCoopSeason.Utils.Hud
{
    public sealed class HudElement
    {
        HudAPIv2.HUDMessage _message;
        string _text;
        double _scale;
        bool _active;
        bool _dirty;
        double _height;

        public void Clear()
        {
            try
            {
                _message?.DeleteMessage();
                _message = null;
                _dirty = true;
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed deleting hud message instance; {e}");
            }
        }

        public HudElement AddTo(HudElementStack group)
        {
            group.Add(this);
            return this;
        }

        public void Apply(string text, double scale = 1d, bool active = true)
        {
            _dirty = false;
            _dirty |= _text != text;
            _dirty |= Math.Abs(_scale - scale) > 0.001;
            _dirty |= _active != active;

            _text = text;
            _scale = scale;
            _active = active;
        }

        public double Render(double offset, bool forceHide = false)
        {
            if (!_dirty && !forceHide) return _height;

            _dirty = false;
            _message?.DeleteMessage();

            if (forceHide) return 0;
            if (!_active) return 0;
            if (string.IsNullOrEmpty(_text)) return 0;

            _message = new HudAPIv2.HUDMessage(
                /*text*/ new StringBuilder(_text),
                /*origin*/ new Vector2D(0f, 1f),
                /*offset*/ new Vector2D(0f, 0f),
                /*time to live*/ -1,
                /*scale*/ _scale,
                /*hide hud*/ true,
                /*shadowing*/ false,
                /*shadow color*/ null,
                /*text*/ MyBillboard.BlendTypeEnum.PostPP);

            var textLength = _message.GetTextLength();
            _message.Offset = new Vector2D(-textLength.X / 2, offset);
            _height = textLength.Y;

            return textLength.Y;
        }

        public static string CreateProgressionBar(double progress)
        {
            var buffer = new StringBuilder();

            for (var i = 0; i < 100; i++)
            {
                var c = (float)i / 100 < progress ? "0,255,0" : "200,0,0";
                buffer.Append($"<color={c}>|");
            }

            buffer.Append("<reset>");
            return buffer.ToString();
        }
    }
}