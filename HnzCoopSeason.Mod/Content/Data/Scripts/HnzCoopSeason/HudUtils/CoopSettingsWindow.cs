using System;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace HnzCoopSeason.HudUtils
{
    public sealed class CoopSettingsWindow : HudElementBase
    {
        const float WindowWidth = 360;
        const float PaddingX = 26;
        const float PaddingY = 18;
        const float RowHeight = 34;
        const float CheckboxSize = 24;
        const float SegmentHeight = 28;
        const float MeterGap = 12;
        const float CloseGlyphSize = 24;
        const float MaxContentHeight = 360; // taller than this and the body scrolls
        const float ScrollBarWidth = 3;
        const float ScrollStep = 28;
        const float Chamfer = 14;
        const float AtlasSize = 64;
        const float CellSize = 32;

        static readonly Color Accent = new Color(187, 233, 246);
        static readonly Color LabelColor = new Color(187, 233, 246, 210);
        static readonly Color HintColor = new Color(187, 233, 246, 110);
        static readonly Color SectionColor = new Color(187, 233, 246, 165);
        static readonly Color SectionRuleColor = new Color(187, 233, 246, 55);

        // created first = drawn behind everything
        readonly TexturedBox[] _plateStrips;
        readonly TexturedBox[] _plateCorners; // TL, TR, BL, BR

        readonly Label _header;
        readonly TexturedBox _headerRule;
        readonly MouseInputElement _dragInput;
        readonly GlyphButton _closeButton;

        // _clip masks to the visible band; _content's offset carries the scroll
        readonly Pane _clip;
        readonly Pane _content;
        readonly TexturedBox _scrollTrack;
        readonly TexturedBox _scrollThumb;
        float _scroll;

        Vector2 _cursorOffset;
        bool _dragging;
        readonly CheckboxRow[] _toggleRows;
        readonly Label _anchorLabel;
        readonly SegmentedSelector _anchorSelector;
        readonly Label _sliderLabel;
        readonly Label _sliderValue;
        readonly SliderBox _slider;
        readonly SectionHeader _capSection;
        readonly Label _fovLabel;
        readonly Label _fovValue;
        readonly SliderBox _fovSlider;
        readonly Label _blocksLabel;
        readonly Label _blocksValue;
        readonly SliderBox _blocksSlider;
        readonly SectionHeader _reticleSection;
        readonly Label _reticleDistLabel;
        readonly Label _reticleDistValue;
        readonly SliderBox _reticleDistSlider;
        readonly Label _hint;

        float _lastSliderValue;
        int _sliderSettleFrames;
        float _lastFovValue;
        int _fovSettleFrames;
        float _lastBlocksValue;
        int _blocksSettleFrames;
        float _lastReticleDistValue;
        int _reticleDistSettleFrames;

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
            // without None it fills the parent and swallows clicks meant for the controls
            _dragInput = new MouseInputElement(this) { DimAlignment = DimAlignments.None };

            _closeButton = new GlyphButton(this, "X", CloseGlyphSize);
            _closeButton.Clicked = Hide;

            _clip = new Pane(this) { IsMasking = true };
            _content = new Pane(_clip) { Size = Vector2.Zero };

            _toggleRows = new[]
            {
                new CheckboxRow(_content, "Progress meter"),
                new CheckboxRow(_content, "Capture meter"),
                new CheckboxRow(_content, "Target reticle"),
                new CheckboxRow(_content, "Hide with WeaponCore"),
                new CheckboxRow(_content, "Minimal meter"),
            };

            _anchorLabel = new Label(_content) { Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f), Text = "Meter anchor" };
            _anchorSelector = new SegmentedSelector(_content, "LEFT", "CENTER", "RIGHT");

            _sliderLabel = new Label(_content) { Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f), Text = "Meter vertical offset" };
            _sliderValue = new Label(_content) { Format = new GlyphFormat(Accent, TextAlignment.Right, 0.9f) };
            _slider = new SliderBox(_content)
            {
                Min = 40,
                Max = 300,
                Width = WindowWidth - PaddingX * 2,
                Height = 36,
            };

            _capSection = new SectionHeader(_content, "CAPMETER");

            _fovLabel = new Label(_content) { Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f), Text = "Target cone" };
            _fovValue = new Label(_content) { Format = new GlyphFormat(Accent, TextAlignment.Right, 0.9f) };
            _fovSlider = new SliderBox(_content)
            {
                Min = 2,
                Max = 45,
                Width = WindowWidth - PaddingX * 2,
                Height = 36,
            };

            _blocksLabel = new Label(_content) { Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f), Text = "Ignore grids under" };
            _blocksValue = new Label(_content) { Format = new GlyphFormat(Accent, TextAlignment.Right, 0.9f) };
            _blocksSlider = new SliderBox(_content)
            {
                Min = 0, // 0 = off
                Max = 500,
                Width = WindowWidth - PaddingX * 2,
                Height = 36,
            };

            _reticleSection = new SectionHeader(_content, "RETICLE");

            _reticleDistLabel = new Label(_content) { Format = new GlyphFormat(LabelColor, TextAlignment.Left, 0.9f), Text = "Hide reticle within" };
            _reticleDistValue = new Label(_content) { Format = new GlyphFormat(Accent, TextAlignment.Right, 0.9f) };
            _reticleDistSlider = new SliderBox(_content)
            {
                Min = 0, // 0 = never hide by distance
                Max = 500,
                Width = WindowWidth - PaddingX * 2,
                Height = 36,
            };

            _scrollTrack = new TexturedBox(this) { Color = new Color(187, 233, 246, 40) };
            _scrollThumb = new TexturedBox(this) { Color = new Color(187, 233, 246, 130) };

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

            _fovSlider.Current = config.ReticleFov;
            _lastFovValue = config.ReticleFov;

            _blocksSlider.Current = config.MinTargetBlocks;
            _lastBlocksValue = config.MinTargetBlocks;

            _reticleDistSlider.Current = config.ReticleMinDistance;
            _lastReticleDistValue = config.ReticleMinDistance;

            _scroll = 0; // reopen at the top

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

            Offset = config.SettingsWindowMoved ? ClampToScreen(config.SettingsWindowOffset) : ParkedUnderMeter();

            var changed = false;
            changed |= Apply(_toggleRows[0].Checkbox.IsBoxChecked, ref config.ShowProgressMeter);
            changed |= Apply(_toggleRows[1].Checkbox.IsBoxChecked, ref config.ShowCaptureMeter);
            changed |= Apply(_toggleRows[2].Checkbox.IsBoxChecked, ref config.ShowTargetReticle);
            changed |= Apply(_toggleRows[3].Checkbox.IsBoxChecked, ref config.HideWithWeaponCore);
            changed |= Apply(_toggleRows[4].Checkbox.IsBoxChecked, ref config.MinimalMeter);

            var anchor = (MeterAnchor)_anchorSelector.SelectedIndex;
            if (anchor != config.MeterAnchor)
            {
                config.MeterAnchor = anchor;
                changed = true;
            }

            // apply live, save once it settles
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

            if (Math.Abs(_fovSlider.Current - _lastFovValue) > 0.25f)
            {
                _lastFovValue = _fovSlider.Current;
                config.ReticleFov = _fovSlider.Current;
                _fovSettleFrames = 45;
                changed = true;
            }
            else if (_fovSettleFrames > 0 && --_fovSettleFrames == 0)
            {
                CoopHud.SaveNow();
            }

            // compare rounded, or every sub-step drag flags a change
            if ((int)_blocksSlider.Current != (int)_lastBlocksValue)
            {
                _lastBlocksValue = _blocksSlider.Current;
                config.MinTargetBlocks = (int)_blocksSlider.Current;
                _blocksSettleFrames = 45;
                changed = true;
            }
            else if (_blocksSettleFrames > 0 && --_blocksSettleFrames == 0)
            {
                CoopHud.SaveNow();
            }

            if ((int)_reticleDistSlider.Current != (int)_lastReticleDistValue)
            {
                _lastReticleDistValue = _reticleDistSlider.Current;
                config.ReticleMinDistance = _reticleDistSlider.Current;
                _reticleDistSettleFrames = 45;
                changed = true;
            }
            else if (_reticleDistSettleFrames > 0 && --_reticleDistSettleFrames == 0)
            {
                CoopHud.SaveNow();
            }

            if (changed) CoopHud.ApplyConfigNow();

            _sliderValue.Text = $"{(int)_slider.Current}px";
            _fovValue.Text = $"{(int)_fovSlider.Current}°";
            _blocksValue.Text = (int)_blocksSlider.Current == 0 ? "off" : $"{(int)_blocksSlider.Current} blocks";
            _reticleDistValue.Text = (int)_reticleDistSlider.Current == 0 ? "off" : $"{(int)_reticleDistSlider.Current}m";

            var left = -WindowWidth / 2 + PaddingX;
            var right = WindowWidth / 2 - PaddingX;
            var y = 0f;

            _header.Offset = new Vector2(left + _header.Width / 2, y - _header.Height / 2);
            _closeButton.Offset = new Vector2(right - CloseGlyphSize / 2, y - _header.Height / 2);

            // grab band stops short of the close button
            var grabWidth = WindowWidth - CloseGlyphSize - PaddingX;
            _dragInput.Size = new Vector2(grabWidth, PaddingY + _header.Height);
            _dragInput.Offset = new Vector2(-(WindowWidth - grabWidth) / 2, (PaddingY - _header.Height) / 2);

            y -= _header.Height + 8;
            _headerRule.Width = WindowWidth - PaddingX * 2;
            _headerRule.Offset = new Vector2(0, y - 1);
            y -= 14;

            // scrolling body, laid out in content space: yc = 0 at the top of the stack
            var viewportTop = y;
            var yc = 0f;

            foreach (var row in _toggleRows)
            {
                row.Layout(left, right, yc, RowHeight, CheckboxSize);
                yc -= RowHeight;
            }

            yc -= 8;
            _anchorLabel.Offset = new Vector2(left + _anchorLabel.Width / 2, yc - _anchorLabel.Height / 2);
            yc -= _anchorLabel.Height + 6;
            _anchorSelector.Layout(left, right, yc, SegmentHeight);
            yc -= SegmentHeight;

            yc -= 8;
            _sliderLabel.Offset = new Vector2(left + _sliderLabel.Width / 2, yc - _sliderLabel.Height / 2);
            _sliderValue.Offset = new Vector2(right - _sliderValue.Width / 2, yc - _sliderValue.Height / 2);
            yc -= MathHelper.Max(_sliderLabel.Height, _sliderValue.Height) + 4;
            _slider.Offset = new Vector2(0, yc - _slider.Height / 2);
            yc -= _slider.Height + 6;

            yc = _capSection.Layout(left, right, yc);

            _fovLabel.Offset = new Vector2(left + _fovLabel.Width / 2, yc - _fovLabel.Height / 2);
            _fovValue.Offset = new Vector2(right - _fovValue.Width / 2, yc - _fovValue.Height / 2);
            yc -= MathHelper.Max(_fovLabel.Height, _fovValue.Height) + 4;
            _fovSlider.Offset = new Vector2(0, yc - _fovSlider.Height / 2);
            yc -= _fovSlider.Height + 6;

            _blocksLabel.Offset = new Vector2(left + _blocksLabel.Width / 2, yc - _blocksLabel.Height / 2);
            _blocksValue.Offset = new Vector2(right - _blocksValue.Width / 2, yc - _blocksValue.Height / 2);
            yc -= MathHelper.Max(_blocksLabel.Height, _blocksValue.Height) + 4;
            _blocksSlider.Offset = new Vector2(0, yc - _blocksSlider.Height / 2);
            yc -= _blocksSlider.Height + 6;

            yc = _reticleSection.Layout(left, right, yc);

            _reticleDistLabel.Offset = new Vector2(left + _reticleDistLabel.Width / 2, yc - _reticleDistLabel.Height / 2);
            _reticleDistValue.Offset = new Vector2(right - _reticleDistValue.Width / 2, yc - _reticleDistValue.Height / 2);
            yc -= MathHelper.Max(_reticleDistLabel.Height, _reticleDistValue.Height) + 4;
            _reticleDistSlider.Offset = new Vector2(0, yc - _reticleDistSlider.Height / 2);
            yc -= _reticleDistSlider.Height + 6;

            var contentHeight = -yc;
            var viewHeight = MathHelper.Min(contentHeight, MaxContentHeight);
            var maxScroll = MathHelper.Max(0, contentHeight - viewHeight);
            _scroll = MathHelper.Clamp(_scroll, 0, maxScroll);

            _clip.Size = new Vector2(WindowWidth, viewHeight);
            _clip.Offset = new Vector2(0, viewportTop - viewHeight / 2);
            _content.Offset = new Vector2(0, viewHeight / 2 + _scroll);

            var scrollable = maxScroll > 0.5f;
            _scrollTrack.Visible = scrollable;
            _scrollThumb.Visible = scrollable;
            if (scrollable)
            {
                var barX = WindowWidth / 2 - PaddingX / 2;
                _scrollTrack.Size = new Vector2(ScrollBarWidth, viewHeight);
                _scrollTrack.Offset = new Vector2(barX, viewportTop - viewHeight / 2);

                // floor the thumb length so it stays grabbable
                var thumbHeight = MathHelper.Max(24, viewHeight * viewHeight / contentHeight);
                var travel = viewHeight - thumbHeight;
                _scrollThumb.Size = new Vector2(ScrollBarWidth, thumbHeight);
                _scrollThumb.Offset = new Vector2(barX, viewportTop - thumbHeight / 2 - travel * (_scroll / maxScroll));
            }

            y = viewportTop - viewHeight - 8;

            _hint.Offset = new Vector2(0, y - _hint.Height / 2);
            y -= _hint.Height;

            LayoutPlate(-y);
        }

        protected override void HandleInput(Vector2 cursorPos)
        {
            if (!Visible) return;

            // bounds tested by hand: the plate hangs PaddingY above the element centre
            var local = cursorPos - (Origin + Offset);
            if (Math.Abs(local.X) <= WindowWidth / 2 && local.Y <= PaddingY && local.Y >= PaddingY - Size.Y)
            {
                if (SharedBinds.MousewheelUp.IsPressed) _scroll -= ScrollStep;
                else if (SharedBinds.MousewheelDown.IsPressed) _scroll += ScrollStep; // clamped in Layout
            }

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

        Vector2 ParkedUnderMeter()
        {
            var meter = CoopHud.Meter;
            if (meter == null) return Vector2.Zero;

            var bottomLeft = meter.PlateBottomLeft;
            return ClampToScreen(new Vector2(bottomLeft.X + WindowWidth / 2, bottomLeft.Y - MeterGap - PaddingY));
        }

        // the plate sits PaddingY above the element center
        Vector2 ClampToScreen(Vector2 offset)
        {
            var halfScreenX = HudMain.ScreenWidth / HudMain.ResScale * 0.5f;
            var halfScreenY = HudMain.ScreenHeight / HudMain.ResScale * 0.5f;

            return new Vector2(
                MathHelper.Clamp(offset.X, -halfScreenX + WindowWidth / 2, halfScreenX - WindowWidth / 2),
                MathHelper.Clamp(offset.Y, -halfScreenY + Size.Y - PaddingY, halfScreenY - PaddingY));
        }

        // bare container; the framework has no plain-node element
        sealed class Pane : HudElementBase
        {
            public Pane(HudParentBase parent) : base(parent)
            {
            }
        }

        sealed class SectionHeader
        {
            const float GapBefore = 10;
            const float GapAfter = 8;
            const float LabelToRule = 10;

            readonly Label _label;
            readonly TexturedBox _rule;

            public SectionHeader(HudParentBase parent, string text)
            {
                _label = new Label(parent) { Format = new GlyphFormat(SectionColor, TextAlignment.Left, 0.82f), Text = text };
                _rule = new TexturedBox(parent) { Color = SectionRuleColor, Height = 1 };
            }

            // returns the new content top, below the divider
            public float Layout(float left, float right, float y)
            {
                y -= GapBefore;

                _label.Offset = new Vector2(left + _label.Width / 2, y - _label.Height / 2);

                var ruleLeft = left + _label.Width + LabelToRule;
                _rule.Width = MathHelper.Max(right - ruleLeft, 1);
                _rule.Offset = new Vector2(ruleLeft + _rule.Width / 2, y - _label.Height / 2);

                return y - (_label.Height + GapAfter);
            }
        }

        static bool Apply(bool value, ref bool target)
        {
            if (value == target) return false;

            target = value;
            return true;
        }

        void LayoutPlate(float contentHeight)
        {
            // alpha follows the game's HUD background opacity, like the meter plate
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

        sealed class GlyphButton : HudElementBase
        {
            static readonly Color IdleFill = new Color(187, 233, 246, 28);
            static readonly Color HoverFill = new Color(187, 233, 246, 90);

            public Action Clicked; // EventHandler is off the SE whitelist

            readonly TexturedBox _box;
            readonly MouseInputElement _input;

            public GlyphButton(HudParentBase parent, string glyph, float size) : base(parent)
            {
                Size = new Vector2(size);

                _box = new TexturedBox(this) { DimAlignment = DimAlignments.Both, Color = IdleFill };

                // ReSharper disable once ObjectCreationAsStatement
                new Label(this) { Format = new GlyphFormat(Accent, TextAlignment.Center, 0.8f), Text = glyph };

                _input = new MouseInputElement(this);
                _input.LeftClicked += (sender, e) => Clicked?.Invoke();
            }

            protected override void Layout()
            {
                _box.Color = _input.IsMousedOver ? HoverFill : IdleFill;
            }
        }

        sealed class SegmentedSelector
        {
            const float SegmentGap = 4;

            static readonly Color IdleFill = new Color(187, 233, 246, 14);
            static readonly Color HoverFill = new Color(187, 233, 246, 45);
            static readonly Color EdgeColor = new Color(187, 233, 246, 255);
            static readonly Color SelectedTextColor = new Color(225, 246, 255);

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
                    var index = i; // capture per iteration
                    var segment = new Button(parent)
                    {
                        Color = IdleFill,
                        HighlightEnabled = false, // built-in highlight would fight the selected state
                    };

                    segment.MouseInput.LeftClicked += (sender, e) => SelectedIndex = index;
                    _segments[i] = segment;

                    _edges[i] = new BorderBox(parent) { Color = EdgeColor, Thickness = 1.5f };

                    // registered last so they draw on top of the segment art
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

                // re-formatting rebuilds the text board
                if (_formattedIndex == SelectedIndex) return;

                for (var i = 0; i < _labels.Length; i++)
                {
                    _labels[i].Format = _labels[i].Format.WithColor(i == SelectedIndex ? SelectedTextColor : LabelColor);
                }

                _formattedIndex = SelectedIndex;
            }
        }

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
