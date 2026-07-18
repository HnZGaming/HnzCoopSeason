using System;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace HnzCoopSeason.HudUtils
{
    /// <summary>
    ///     Custom HUD settings window in the mod's own design language (chamfered plate,
    ///     vanilla-cyan accent, flush-aligned rows) — replaces the Rich HUD Terminal page
    ///     whose tile layout/spacing is hard-coded in the Master mod and cannot be styled.
    /// </summary>
    public sealed class CoopSettingsWindow : HudElementBase
    {
        const float WindowWidth = 360;
        const float PaddingX = 26;
        const float PaddingY = 18;
        const float RowHeight = 34;
        const float CheckboxSize = 24;
        const float SegmentHeight = 28;
        const float MeterGap = 12; // vertical gap under the meter plate when the window is parked there
        const float CloseGlyphSize = 24;
        const float Chamfer = 14;
        const float AtlasSize = 64;
        const float CellSize = 32;

        static readonly Color Accent = new Color(187, 233, 246);
        static readonly Color LabelColor = new Color(187, 233, 246, 210);
        static readonly Color HintColor = new Color(187, 233, 246, 110);

        // plate (created first = drawn behind everything)
        readonly TexturedBox[] _plateStrips; // top band / middle / bottom band
        readonly TexturedBox[] _plateCorners; // TL, TR, BL, BR

        readonly Label _header;
        readonly TexturedBox _headerRule;
        readonly MouseInputElement _dragInput; // grab area: the header band
        readonly GlyphButton _closeButton;

        Vector2 _cursorOffset;
        bool _dragging;
        readonly CheckboxRow[] _toggleRows;
        readonly Label _anchorLabel;
        readonly SegmentedSelector _anchorSelector;
        readonly Label _sliderLabel;
        readonly Label _sliderValue;
        readonly SliderBox _slider;
        readonly Label _hint;

        float _lastSliderValue;
        int _sliderSettleFrames;

        public CoopSettingsWindow(HudParentBase parent) : base(parent)
        {
            _plateStrips = new TexturedBox[3];
            for (var i = 0; i < 3; i++)
            {
                _plateStrips[i] = new TexturedBox(this) { Color = MeterPanel.BackColor };
            }

            var cells = new[] { new Vector2(0, 0), new Vector2(CellSize, 0), new Vector2(0, CellSize), new Vector2(CellSize, CellSize) };
            _plateCorners = new TexturedBox[4];
            for (var i = 0; i < 4; i++)
            {
                _plateCorners[i] = new TexturedBox(this)
                {
                    Color = MeterPanel.BackColor,
                    Material = new Material("CoopHudCorners", new Vector2(AtlasSize), cells[i], new Vector2(CellSize)),
                    Size = new Vector2(Chamfer),
                };
            }

            _header = new Label(this) { Format = new GlyphFormat(Accent, TextAlignment.Left, 1.05f), Text = "HUD SETTINGS" };
            _headerRule = new TexturedBox(this) { Color = new Color(187, 233, 246, 90), Height = 2 };
            // DimAlignments.None: MouseInputElement defaults to filling its parent, which would
            // make the whole window a drag handle and swallow clicks meant for the controls
            _dragInput = new MouseInputElement(this) { DimAlignment = DimAlignments.None };

            _closeButton = new GlyphButton(this, "X", CloseGlyphSize);
            _closeButton.Clicked = Hide;

            _toggleRows = new[]
            {
                new CheckboxRow(this, "Progress meter"),
                new CheckboxRow(this, "Capture meter"),
                new CheckboxRow(this, "Target reticle"),
                new CheckboxRow(this, "Hide with WeaponCore"),
                new CheckboxRow(this, "Minimal meter"),
            };

            _anchorLabel = new Label(this) { Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f), Text = "Meter anchor" };
            _anchorSelector = new SegmentedSelector(this, "LEFT", "CENTER", "RIGHT");

            _sliderLabel = new Label(this) { Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f), Text = "Meter vertical offset" };
            _sliderValue = new Label(this) { Format = new GlyphFormat(Accent, TextAlignment.Right, 0.9f) };
            _slider = new SliderBox(this)
            {
                Min = 40,
                Max = 300,
                Width = WindowWidth - PaddingX * 2,
                Height = 36,
            };

            _hint = new Label(this) { Format = new GlyphFormat(HintColor, TextAlignment.Center, 0.72f), Text = "drag the header to move" };

            Visible = false;
        }

        public void Show()
        {
            var config = CoopHud.Config;
            _toggleRows[0].Checkbox.IsBoxChecked = config.ShowProgressMeter;
            _toggleRows[1].Checkbox.IsBoxChecked = config.ShowCaptureMeter;
            _toggleRows[2].Checkbox.IsBoxChecked = config.ShowTargetReticle;
            _toggleRows[3].Checkbox.IsBoxChecked = config.HideWithWeaponCore;
            _toggleRows[4].Checkbox.IsBoxChecked = config.MinimalMeter;
            _anchorSelector.SelectedIndex = (int)config.MeterAnchor;
            _slider.Current = config.MeterTopMargin;
            _lastSliderValue = config.MeterTopMargin;

            Visible = true;
            HudMain.EnableCursor = true;
        }

        public void Hide()
        {
            if (!Visible) return;

            Visible = false;
            HudMain.EnableCursor = false;
            CoopHud.SaveNow();
        }

        public void Close()
        {
            Unregister();
        }

        public bool IsOpen => Visible;

        public void Toggle()
        {
            if (Visible) Hide();
            else Show();
        }

        protected override void Layout()
        {
            if (!Visible) return;

            var config = CoopHud.Config;

            // place the window: stored position once dragged, otherwise parked under the meter
            Offset = config.SettingsWindowMoved ? ClampToScreen(config.SettingsWindowOffset) : ParkedUnderMeter();

            // sync checkbox clicks -> config
            var changed = false;
            changed |= Apply(_toggleRows[0].Checkbox.IsBoxChecked, ref config.ShowProgressMeter);
            changed |= Apply(_toggleRows[1].Checkbox.IsBoxChecked, ref config.ShowCaptureMeter);
            changed |= Apply(_toggleRows[2].Checkbox.IsBoxChecked, ref config.ShowTargetReticle);
            changed |= Apply(_toggleRows[3].Checkbox.IsBoxChecked, ref config.HideWithWeaponCore);
            changed |= Apply(_toggleRows[4].Checkbox.IsBoxChecked, ref config.MinimalMeter);

            // anchor segments -> config
            var anchor = (MeterAnchor)_anchorSelector.SelectedIndex;
            if (anchor != config.MeterAnchor)
            {
                config.MeterAnchor = anchor;
                changed = true;
            }

            // slider: apply live, save once it settles
            if (Math.Abs(_slider.Current - _lastSliderValue) > 0.5f)
            {
                _lastSliderValue = _slider.Current;
                config.MeterTopMargin = _slider.Current;
                _sliderSettleFrames = 45;
                changed = true;
            }
            else if (_sliderSettleFrames > 0 && --_sliderSettleFrames == 0)
            {
                CoopHud.SaveNow();
            }

            if (changed) CoopHud.ApplyConfigNow();

            _sliderValue.Text = $"{(int)_slider.Current}px";

            // content stack: header, rule, checkbox rows, slider block, hint
            var left = -WindowWidth / 2 + PaddingX;
            var right = WindowWidth / 2 - PaddingX;
            var y = 0f; // content top; the plate wraps around the final extent

            _header.Offset = new Vector2(left + _header.Width / 2, y - _header.Height / 2);
            _closeButton.Offset = new Vector2(right - CloseGlyphSize / 2, y - _header.Height / 2);

            // grab band: the header row plus the padding above it, stopping short of the close button
            var grabWidth = WindowWidth - CloseGlyphSize - PaddingX;
            _dragInput.Size = new Vector2(grabWidth, PaddingY + _header.Height);
            _dragInput.Offset = new Vector2(-(WindowWidth - grabWidth) / 2, (PaddingY - _header.Height) / 2);

            y -= _header.Height + 8;
            _headerRule.Width = WindowWidth - PaddingX * 2;
            _headerRule.Offset = new Vector2(0, y - 1);
            y -= 14;

            foreach (var row in _toggleRows)
            {
                row.Layout(left, right, y, RowHeight, CheckboxSize);
                y -= RowHeight;
            }

            y -= 8;
            _anchorLabel.Offset = new Vector2(left + _anchorLabel.Width / 2, y - _anchorLabel.Height / 2);
            y -= _anchorLabel.Height + 6;
            _anchorSelector.Layout(left, right, y, SegmentHeight);
            y -= SegmentHeight;

            y -= 8;
            _sliderLabel.Offset = new Vector2(left + _sliderLabel.Width / 2, y - _sliderLabel.Height / 2);
            _sliderValue.Offset = new Vector2(right - _sliderValue.Width / 2, y - _sliderValue.Height / 2);
            y -= MathHelper.Max(_sliderLabel.Height, _sliderValue.Height) + 4;
            _slider.Offset = new Vector2(0, y - _slider.Height / 2);
            y -= _slider.Height + 6;

            _hint.Offset = new Vector2(0, y - _hint.Height / 2);
            y -= _hint.Height;

            LayoutPlate(-y);
        }

        /// <summary>
        ///     Grab-offset drag on the header band: capture (position - cursor) on click and
        ///     re-apply it every frame while held, so the window never drifts from the grab
        ///     point. Position is written live for WYSIWYG; the disk write waits for release.
        /// </summary>
        protected override void HandleInput(Vector2 cursorPos)
        {
            if (!Visible) return;

            if (_dragInput.IsNewLeftClicked)
            {
                _dragging = true;
                _cursorOffset = Origin + Offset - cursorPos;
            }

            if (!_dragging) return;

            if (!SharedBinds.LeftButton.IsPressed)
            {
                _dragging = false;
                CoopHud.SaveNow();
                return;
            }

            var config = CoopHud.Config;
            config.SettingsWindowOffset = ClampToScreen(cursorPos + _cursorOffset - Origin);
            config.SettingsWindowMoved = true;
        }

        /// <summary>Default spot: tucked under the meter plate, left edges flush.</summary>
        Vector2 ParkedUnderMeter()
        {
            var meter = CoopHud.Meter;
            if (meter == null) return Vector2.Zero;

            var bottomLeft = meter.PlateBottomLeft;
            return ClampToScreen(new Vector2(bottomLeft.X + WindowWidth / 2, bottomLeft.Y - MeterGap - PaddingY));
        }

        /// <summary>Keeps the whole plate on screen. The plate sits PaddingY above the element center.</summary>
        Vector2 ClampToScreen(Vector2 offset)
        {
            var halfScreenX = HudMain.ScreenWidth / HudMain.ResScale * 0.5f;
            var halfScreenY = HudMain.ScreenHeight / HudMain.ResScale * 0.5f;

            return new Vector2(
                MathHelper.Clamp(offset.X, -halfScreenX + WindowWidth / 2, halfScreenX - WindowWidth / 2),
                MathHelper.Clamp(offset.Y, -halfScreenY + Size.Y - PaddingY, halfScreenY - PaddingY));
        }

        static bool Apply(bool value, ref bool target)
        {
            if (value == target) return false;

            target = value;
            return true;
        }

        void LayoutPlate(float contentHeight)
        {
            // plate reads exactly like the meter's: same RGB, alpha driven by the game's
            // HUD background opacity setting, so both plates sit at the same weight
            var opacity = MathHelper.Clamp(Sandbox.ModAPI.MyAPIGateway.Session.Config.HUDBkOpacity, 0f, 1f);
            var plateColor = new Color(MeterPanel.BackColor.R, MeterPanel.BackColor.G, MeterPanel.BackColor.B, (byte)(opacity * 255));

            foreach (var strip in _plateStrips)
            {
                strip.Color = plateColor;
            }

            foreach (var corner in _plateCorners)
            {
                corner.Color = plateColor;
            }

            var plateHeight = contentHeight + PaddingY * 2;
            var top = PaddingY;
            var bottom = top - plateHeight;
            Size = new Vector2(WindowWidth, plateHeight);

            _plateStrips[0].Size = new Vector2(WindowWidth - Chamfer * 2, Chamfer);
            _plateStrips[0].Offset = new Vector2(0, top - Chamfer / 2);
            _plateStrips[1].Size = new Vector2(WindowWidth, plateHeight - Chamfer * 2);
            _plateStrips[1].Offset = new Vector2(0, (top + bottom) / 2);
            _plateStrips[2].Size = new Vector2(WindowWidth - Chamfer * 2, Chamfer);
            _plateStrips[2].Offset = new Vector2(0, bottom + Chamfer / 2);

            for (var i = 0; i < 4; i++)
            {
                var sx = i % 2 == 0 ? -1 : 1;
                var isTop = i < 2;
                _plateCorners[i].Offset = new Vector2(
                    sx * (WindowWidth - Chamfer) / 2,
                    isTop ? top - Chamfer / 2 : bottom + Chamfer / 2);
            }
        }

        /// <summary>
        ///     Small square action button: flat box with a centered accent glyph,
        ///     brightening on hover.
        /// </summary>
        sealed class GlyphButton : HudElementBase
        {
            static readonly Color IdleFill = new Color(187, 233, 246, 28);
            static readonly Color HoverFill = new Color(187, 233, 246, 90);

            public Action Clicked; // Action, not EventHandler — EventHandler is off the SE whitelist

            readonly TexturedBox _box;
            readonly MouseInputElement _input;

            public GlyphButton(HudParentBase parent, string glyph, float size) : base(parent)
            {
                Size = new Vector2(size);

                _box = new TexturedBox(this) { DimAlignment = DimAlignments.Both, Color = IdleFill };

                // ReSharper disable once ObjectCreationAsStatement — the glyph parents itself
                new Label(this) { Format = new GlyphFormat(Accent, TextAlignment.Center, 0.8f), Text = glyph };

                _input = new MouseInputElement(this);
                _input.LeftClicked += (sender, e) => Clicked?.Invoke();
            }

            protected override void Layout()
            {
                _box.Color = _input.IsMousedOver ? HoverFill : IdleFill;
            }
        }

        /// <summary>
        ///     Segmented option row — the whole choice is visible and one click wide:
        ///     [ LEFT ][ CENTER ][ RIGHT ]. The selected segment gets a single bright edge
        ///     rather than a solid fill — a fill this bright leaves the label unreadable
        ///     at any text colour.
        ///     (A dropdown would hide two of three options behind an extra click and drop
        ///     an unstyled Rich HUD list over this window's plate.)
        /// </summary>
        sealed class SegmentedSelector
        {
            const float SegmentGap = 4;

            static readonly Color IdleFill = new Color(187, 233, 246, 14);
            static readonly Color HoverFill = new Color(187, 233, 246, 45);
            static readonly Color EdgeColor = new Color(187, 233, 246, 255);
            static readonly Color SelectedTextColor = new Color(225, 246, 255); // brightest text in the row

            public int SelectedIndex;

            readonly Button[] _segments;
            readonly BorderBox[] _edges;
            readonly Label[] _labels;
            int _formattedIndex = -1;

            public SegmentedSelector(HudParentBase parent, params string[] options)
            {
                _segments = new Button[options.Length];
                _edges = new BorderBox[options.Length];
                _labels = new Label[options.Length];

                for (var i = 0; i < options.Length; i++)
                {
                    var index = i; // capture per iteration for the click handler
                    var segment = new Button(parent)
                    {
                        Color = IdleFill,
                        HighlightEnabled = false, // built-in highlight swaps Color and would fight the selected state
                    };

                    segment.MouseInput.LeftClicked += (sender, e) => SelectedIndex = index;
                    _segments[i] = segment;

                    _edges[i] = new BorderBox(parent) { Color = EdgeColor, Thickness = 1.5f };

                    // labels are registered last, so they draw on top of the segment art
                    _labels[i] = new Label(parent)
                    {
                        Format = new GlyphFormat(LabelColor, TextAlignment.Center, 0.82f),
                        Text = options[i],
                    };
                }
            }

            public void Layout(float left, float right, float y, float height)
            {
                var segmentWidth = (right - left - SegmentGap * (_segments.Length - 1)) / _segments.Length;
                var centerY = y - height / 2;

                for (var i = 0; i < _segments.Length; i++)
                {
                    var center = new Vector2(left + i * (segmentWidth + SegmentGap) + segmentWidth / 2, centerY);
                    var size = new Vector2(segmentWidth, height);
                    var selected = i == SelectedIndex;

                    _segments[i].Size = size;
                    _segments[i].Offset = center;
                    _segments[i].Color = _segments[i].IsMousedOver ? HoverFill : IdleFill;

                    _edges[i].Visible = selected;
                    _edges[i].Size = size;
                    _edges[i].Offset = center;

                    _labels[i].Offset = center;
                }

                // re-formatting rebuilds the text board; only touch it when the selection moves
                if (_formattedIndex == SelectedIndex) return;

                for (var i = 0; i < _labels.Length; i++)
                {
                    _labels[i].Format = _labels[i].Format.WithColor(i == SelectedIndex ? SelectedTextColor : LabelColor);
                }

                _formattedIndex = SelectedIndex;
            }
        }

        /// <summary>
        ///     One settings row: label flush-left, checkbox flush-right — the alignment the
        ///     Rich HUD terminal can't do.
        /// </summary>
        sealed class CheckboxRow
        {
            public readonly BorderedCheckBox Checkbox;
            readonly Label _label;

            public CheckboxRow(HudParentBase parent, string text)
            {
                _label = new Label(parent)
                {
                    Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f),
                    Text = text,
                };

                Checkbox = new BorderedCheckBox(parent)
                {
                    Size = new Vector2(CheckboxSize),
                };
            }

            public void Layout(float left, float right, float y, float rowHeight, float boxSize)
            {
                var centerY = y - rowHeight / 2;
                _label.Offset = new Vector2(left + _label.Width / 2, centerY);
                Checkbox.Size = new Vector2(boxSize);
                Checkbox.Offset = new Vector2(right - boxSize / 2, centerY);
            }
        }
    }
}
