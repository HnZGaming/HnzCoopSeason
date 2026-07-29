using System;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using VRageMath;

namespace HnzCoopSeason.HudUtils
{
    /// <summary>Screen-top meter panel; reads the winning MeterState from ScreenTopHud every frame.</summary>
    public sealed class MeterPanel : HudElementBase
    {
        const float LineSpacing = 7;
        const float ProseSpacing = 1;
        const float BackPaddingX = 18;
        const float BackPaddingY = 14;
        const float BottomTrimPadding = 17;
        const float InfoIconSize = 17;
        const float InfoIconGap = 7;
        const float InfoIconNudge = 0; // badge sits on the title's centre line
        const float MinimalRailPadding = 5;
        const float TitleValueGap = 12;
        const int MaxTitleProbes = 8; // each probe rebuilds the title's text board

        internal static readonly Color VanillaCyan = new Color(187, 233, 246);
        internal static readonly Color BackColor = new Color(33, 44, 51);
        static readonly Color TitleColor = VanillaCyan;
        static readonly Color DescriptionColor = new Color(187, 233, 246, 150);
        static readonly Color SubtitleColor = DescriptionColor;
        static readonly Color SubtitleHighlightColor = new Color(232, 90, 70);

        public float TopMargin = 50;
        public MeterAnchor Anchor = MeterAnchor.Left;
        public bool Minimal; // title + bar only, no prose lines
        const float SideMargin = 30;

        readonly ChamferedPlate _background;
        readonly InfoIcon _infoIcon;
        readonly Label _titleLabel;
        readonly Label _valueLabel;
        readonly CapsuleBar _bar;
        readonly DotRail _dotRail;
        readonly Label _nameLabel;
        readonly Label _subtitleLabel;
        readonly Label _descriptionLabel;

        float _plateHeight = 120;
        string _titleSource;
        float _titleBudget;
        string _valueCache, _nameCache, _subtitleCache, _descriptionCache;

        public MeterPanel(HudParentBase parent) : base(parent)
        {
            Size = Vector2.Zero;

            _background = new ChamferedPlate(this, BackColor); // created first so it draws behind everything

            _infoIcon = new InfoIcon(this, InfoIconSize);

            _titleLabel = new Label(this)
            {
                Format = new GlyphFormat(TitleColor, TextAlignment.Left, 1.0f),
            };

            _valueLabel = new Label(this)
            {
                Format = new GlyphFormat(VanillaCyan, TextAlignment.Right, 0.88f),
            };

            _bar = new CapsuleBar(this);
            _dotRail = new DotRail(this);

            _nameLabel = new Label(this)
            {
                Format = new GlyphFormat(new Color(187, 233, 246, 200), TextAlignment.Left, 0.76f),
            };

            _subtitleLabel = new Label(this)
            {
                Format = new GlyphFormat(SubtitleColor, TextAlignment.Left, 0.8f),
            };

            _descriptionLabel = new Label(this)
            {
                Format = new GlyphFormat(DescriptionColor, TextAlignment.Left, 0.8f),
            };
        }

        protected override void Layout()
        {
            // hide whenever the vanilla hud is hidden
            var config = MyAPIGateway.Session.Config;
            var hudVisible = !config.MinimalHud && !MyAPIGateway.Gui.IsCursorVisible;
            var state = hudVisible ? ScreenTopHud.Instance.Current : null;
            SetContentVisible(state != null);
            if (state == null) return;

            var opacity = MathHelper.Clamp(config.HUDBkOpacity, 0f, 1f);
            _background.SetColor(new Color(BackColor.R, BackColor.G, BackColor.B, (byte)(opacity * 255)));

            Offset = new Vector2(
                AnchorOffsetX(),
                HudMain.ScreenHeight / HudMain.ResScale * 0.5f - TopMargin);

            _bar.SetProgress(state.Progress, state.HealthStyle, state.CompleteText);

            const float halfWidth = CapsuleBar.BarWidth / 2;

            var y = 0f;
            // value first: what it leaves is the title's width budget
            _valueLabel.Visible = true;
            SetTextIfChanged(_valueLabel, state.ValueText ?? "", ref _valueCache);

            _infoIcon.Visible = state.ShowInfoIcon;
            var titleLeft = -halfWidth + (state.ShowInfoIcon ? InfoIconSize + InfoIconGap : 0);

            _titleLabel.Visible = true;
            ApplyTitle(state.Title ?? "", halfWidth - _valueLabel.Width - TitleValueGap - titleLeft);
            _titleLabel.Offset = new Vector2(titleLeft + _titleLabel.Width / 2, y - _titleLabel.Height / 2);

            if (state.ShowInfoIcon)
            {
                // align to the title's centre line, not the row top
                _infoIcon.Offset = new Vector2(-halfWidth + InfoIconSize / 2, _titleLabel.Offset.Y - InfoIconNudge);
            }

            _valueLabel.Offset = new Vector2(halfWidth - _valueLabel.Width / 2, y - _valueLabel.Height / 2);

            y -= MathHelper.Max(_titleLabel.Height, _valueLabel.Height) + LineSpacing;

            _bar.Offset = new Vector2(0, y - _bar.Height / 2);
            y -= _bar.Height + 5;

            _dotRail.Offset = new Vector2(0, y - _dotRail.Height / 2 - 3);
            y -= _dotRail.Height + 5;

            y = StackSubtitle(Minimal ? "" : state.Subtitle, Minimal ? null : state.SubtitleHighlight, y, ProseSpacing);
            y = Stack(_descriptionLabel, Minimal ? "" : state.Description, y, ref _descriptionCache);

            // in minimal mode the rail closes the stack, so pad instead of trimming to a text line
            var contentHeight = -y - LineSpacing + (Minimal ? MinimalRailPadding : 0);
            var mainSize = new Vector2(CapsuleBar.BarWidth + BackPaddingX * 2, contentHeight + BackPaddingY + BottomTrimPadding);
            var mainBottom = -(contentHeight + BottomTrimPadding);

            _nameLabel.Visible = true;
            SetTextIfChanged(_nameLabel, state.BarName ?? "", ref _nameCache);
            const float tabHeight = 14;
            var tabWidth = BackPaddingX + _nameLabel.Width + 26;
            _nameLabel.Offset = new Vector2(-halfWidth + _nameLabel.Width / 2, mainBottom + 2);

            _background.SetPlateSize(mainSize, tabWidth, tabHeight, Minimal);
            _background.Offset = new Vector2(0, BackPaddingY - (mainSize.Y + tabHeight) / 2);
            _plateHeight = mainSize.Y + tabHeight;
        }

        /// <summary>Bottom-left corner of the drawn plate in HighDpiRoot space; valid while hidden too.</summary>
        public Vector2 PlateBottomLeft => new Vector2(
            AnchorOffsetX() - (CapsuleBar.BarWidth / 2 + BackPaddingX),
            HudMain.ScreenHeight / HudMain.ResScale * 0.5f - TopMargin + BackPaddingY - _plateHeight);

        /// <summary>X of the anchor point; children are centred on it, so a side anchor shifts inward by half the panel width.</summary>
        float AnchorOffsetX()
        {
            if (Anchor == MeterAnchor.Center) return 0;

            const float panelHalfWidth = CapsuleBar.BarWidth / 2 + BackPaddingX;
            var inset = HudMain.ScreenWidth / HudMain.ResScale * 0.5f - SideMargin - panelHalfWidth;
            return Anchor == MeterAnchor.Left ? -inset : inset;
        }

        /// <summary>Alert badge; drawn white so the texture keeps its own colours.</summary>
        sealed class InfoIcon : HudElementBase
        {
            static readonly Material AlertMat = new Material("CoopHudAlert", new Vector2(512));

            public InfoIcon(HudParentBase parent, float size) : base(parent)
            {
                Size = new Vector2(size);

                // ReSharper disable once ObjectCreationAsStatement
                new TexturedBox(this)
                {
                    DimAlignment = DimAlignments.Both,
                    Color = Color.White,
                    Material = AlertMat,
                };
            }
        }

        /// <summary>Assigns label text only when it changed; RichHud re-parses every glyph on assignment.</summary>
        static void SetTextIfChanged(Label label, string text, ref string cache)
        {
            if (text == cache) return;

            cache = text;
            label.Text = text;
        }

        /// <summary>Sets the title, truncating with an ellipsis if it would run past the % readout.</summary>
        void ApplyTitle(string text, float budget)
        {
            if (text == _titleSource && Math.Abs(budget - _titleBudget) < 0.5f) return;

            _titleSource = text;
            _titleBudget = budget;

            _titleLabel.Text = text;
            if (budget <= 0 || _titleLabel.Width <= budget) return;

            // every probe re-parses the text board, so guess the length proportionally
            var keep = Math.Max(1, (int)(text.Length * budget / _titleLabel.Width) - 1);
            for (var probe = 0; probe < MaxTitleProbes && keep > 1; probe++)
            {
                _titleLabel.Text = Ellipsize(text, keep);
                if (_titleLabel.Width <= budget) return;

                var next = (int)(keep * budget / _titleLabel.Width);
                keep = next < keep ? Math.Max(1, next) : keep - 1;
            }

            _titleLabel.Text = Ellipsize(text, keep);
        }

        static string Ellipsize(string text, int keep)
        {
            return text.Substring(0, keep).TrimEnd() + "...";
        }

        void SetContentVisible(bool visible)
        {
            _background.Visible = visible;
            _infoIcon.Visible = visible;
            _titleLabel.Visible = visible;
            _valueLabel.Visible = visible;
            _bar.Visible = visible;
            _dotRail.Visible = visible;
            _nameLabel.Visible = visible;
            _subtitleLabel.Visible = visible;
            _descriptionLabel.Visible = visible;
        }

        // highlight phrase is drawn in the alert colour, so the line is built as RichText segments
        float StackSubtitle(string text, string highlight, float y, float spacing)
        {
            if (string.IsNullOrEmpty(text))
            {
                _subtitleLabel.Visible = false;
                return y;
            }

            _subtitleLabel.Visible = true;

            var cacheKey = text + " " + highlight;
            if (cacheKey != _subtitleCache)
            {
                _subtitleCache = cacheKey;

                var at = string.IsNullOrEmpty(highlight) ? -1 : text.IndexOf(highlight, StringComparison.Ordinal);
                if (at < 0)
                {
                    _subtitleLabel.Text = text;
                }
                else
                {
                    var body = _subtitleLabel.Format;
                    var alert = body.WithColor(SubtitleHighlightColor);

                    var rich = new RichText();
                    if (at > 0) rich.Add(text.Substring(0, at), body);
                    rich.Add(highlight, alert);
                    var tail = at + highlight.Length;
                    if (tail < text.Length) rich.Add(text.Substring(tail), body);

                    _subtitleLabel.TextBoard.SetText(rich);
                }
            }

            _subtitleLabel.Offset = new Vector2(-CapsuleBar.BarWidth / 2 + _subtitleLabel.Width / 2, y - _subtitleLabel.Height / 2);
            return y - (_subtitleLabel.Height + spacing);
        }

        float Stack(Label label, string text, float y, ref string cache)
        {
            if (string.IsNullOrEmpty(text))
            {
                label.Visible = false;
                return y;
            }

            label.Visible = true;
            SetTextIfChanged(label, text, ref cache);

            // Width is only correct once Text is assigned
            label.Offset = new Vector2(-CapsuleBar.BarWidth / 2 + label.Width / 2, y - label.Height / 2);
            return y - (label.Height + LineSpacing);
        }

        /// <summary>
        ///     Dark translucent plate; smooth chamfers from a pre-baked anti-aliased DDS
        ///     (CoopHudCorners atlas, one quadrant per corner, Data\TransparentMaterials.sbc).
        ///     Silhouette: main body over the title/bar/rail, then the background trims away
        ///     except a bottom-left TAB that holds the meter name, joined by a 45-degree ramp:
        ///     ╭──────────────────────────────╮
        ///     │  title / bar / dotted rail   │
        ///     ╰────────╮_____________________╯
        ///     │ name  ╱
        ///     ╰──────╯
        ///     Drawn as adjacent strips (no overlaps — double-drawn translucency shows seams).
        /// </summary>
        sealed class ChamferedPlate : HudElementBase
        {
            const float Chamfer = 14;
            const float BottomRightChamfer = Chamfer * 2.25f;
            const float AtlasSize = 64;
            const float CellSize = 32;

            readonly TexturedBox _topStrip;
            readonly TexturedBox _middle;
            readonly TexturedBox _mainBottomStrip;
            readonly TexturedBox _tabStrip;
            readonly TexturedBox _tabLeftFill;
            readonly TexturedBox _tabRightFill;
            readonly TexturedBox[] _corners; // TL, TR, main-BR, tab-BL, tab ramp
            Color _color;

            public ChamferedPlate(HudParentBase parent, Color color) : base(parent)
            {
                _topStrip = new TexturedBox(this) { Color = color };
                _middle = new TexturedBox(this) { Color = color };
                _mainBottomStrip = new TexturedBox(this) { Color = color };
                _tabStrip = new TexturedBox(this) { Color = color };
                _tabLeftFill = new TexturedBox(this) { Color = color };
                _tabRightFill = new TexturedBox(this) { Color = color };

                // atlas cells: TL(0,0) TR(1,0) BL(0,1) BR(1,1)
                var cells = new[]
                {
                    new Vector2(0, 0),
                    new Vector2(CellSize, 0),
                    new Vector2(CellSize, CellSize),
                    new Vector2(0, CellSize),
                    new Vector2(CellSize, CellSize),
                };

                _corners = new TexturedBox[cells.Length];
                for (var i = 0; i < cells.Length; i++)
                {
                    _corners[i] = new TexturedBox(this)
                    {
                        Color = color,
                        Material = new Material("CoopHudCorners", new Vector2(AtlasSize), cells[i], new Vector2(CellSize)),
                    };
                }
            }

            public void SetColor(Color color)
            {
                if (color == _color) return;

                _color = color;
                _topStrip.Color = color;
                _middle.Color = color;
                _mainBottomStrip.Color = color;
                _tabStrip.Color = color;
                _tabLeftFill.Color = color;
                _tabRightFill.Color = color;
                foreach (var corner in _corners)
                {
                    corner.Color = color;
                }
            }

            /// <param name="minimal">true to square the bottom-right cut back to the others</param>
            public void SetPlateSize(Vector2 mainSize, float tabWidth, float tabHeight, bool minimal)
            {
                var bottomRight = minimal ? Chamfer : BottomRightChamfer;

                Size = new Vector2(mainSize.X, mainSize.Y + tabHeight);

                var left = -mainSize.X / 2;
                var top = mainSize.Y / 2 + tabHeight / 2;
                var mainBottom = top - mainSize.Y;

                // top chamfer band
                _topStrip.Size = new Vector2(mainSize.X - Chamfer * 2, Chamfer);
                _topStrip.Offset = new Vector2(0, top - Chamfer / 2);
                _corners[0].Size = new Vector2(Chamfer);
                _corners[0].Offset = new Vector2(left + Chamfer / 2, top - Chamfer / 2);
                _corners[1].Size = new Vector2(Chamfer);
                _corners[1].Offset = new Vector2(-left - Chamfer / 2, top - Chamfer / 2);

                _middle.Size = new Vector2(mainSize.X, mainSize.Y - Chamfer - bottomRight);
                _middle.Offset = new Vector2(0, top - Chamfer - _middle.Height / 2);

                // main bottom band: square left (continues into the tab), chamfered right
                _mainBottomStrip.Size = new Vector2(mainSize.X - bottomRight, bottomRight);
                _mainBottomStrip.Offset = new Vector2(left + _mainBottomStrip.Width / 2, mainBottom + bottomRight / 2);
                _corners[2].Size = new Vector2(bottomRight);
                _corners[2].Offset = new Vector2(-left - bottomRight / 2, mainBottom + bottomRight / 2);

                var tabBottom = mainBottom - tabHeight;
                _tabStrip.Size = new Vector2(tabWidth - Chamfer * 2, tabHeight);
                _tabStrip.Offset = new Vector2(left + Chamfer + _tabStrip.Width / 2, mainBottom - tabHeight / 2);
                _tabLeftFill.Size = new Vector2(Chamfer, tabHeight - Chamfer);
                _tabLeftFill.Offset = new Vector2(left + Chamfer / 2, mainBottom - (tabHeight - Chamfer) / 2);
                _tabRightFill.Size = new Vector2(Chamfer, tabHeight - Chamfer);
                _tabRightFill.Offset = new Vector2(left + tabWidth - Chamfer / 2, mainBottom - (tabHeight - Chamfer) / 2);
                _corners[3].Size = new Vector2(Chamfer);
                _corners[3].Offset = new Vector2(left + Chamfer / 2, tabBottom + Chamfer / 2);
                _corners[4].Size = new Vector2(Chamfer);
                _corners[4].Offset = new Vector2(left + tabWidth - Chamfer / 2, tabBottom + Chamfer / 2);
            }
        }

        /// <summary>Outlined capsule track with an inset fill and a bright end block.</summary>
        sealed class CapsuleBar : HudElementBase
        {
            public const float BarWidth = 430;
            const float BarHeight = 20;
            const float OutlineThickness = 1.5f;
            const float FillInset = 6;
            const float CapWidth = 4;

            static readonly Color OutlineColor = new Color(187, 233, 246, 220);
            static readonly Color FillColor = VanillaCyan;
            static readonly Color CapColor = new Color(240, 252, 255);

            static readonly Color FullOutlineColor = new Color(46, 204, 64, 220);
            static readonly Color FullFillColor = new Color(46, 204, 64);
            static readonly Color FullCapColor = new Color(150, 240, 165);

            static readonly Color HealthOutlineColor = new Color(232, 90, 70, 220);
            static readonly Color HealthFillColor = new Color(232, 90, 70);
            static readonly Color HealthCapColor = new Color(255, 150, 135);

            readonly BorderBox _outline;
            readonly TexturedBox _fill;
            readonly TexturedBox _cap;
            readonly Label _completeLabel;
            string _completeCache;

            public CapsuleBar(HudParentBase parent) : base(parent)
            {
                Size = new Vector2(BarWidth, BarHeight);

                _outline = new BorderBox(this)
                {
                    Size = new Vector2(BarWidth, BarHeight),
                    Color = OutlineColor,
                    Thickness = OutlineThickness,
                };

                _fill = new TexturedBox(this)
                {
                    ParentAlignment = ParentAlignments.Left | ParentAlignments.InnerH,
                    Height = BarHeight - FillInset * 2,
                    Color = FillColor,
                };

                _cap = new TexturedBox(this)
                {
                    ParentAlignment = ParentAlignments.Left | ParentAlignments.InnerH,
                    Size = new Vector2(CapWidth, BarHeight - FillInset * 2),
                    Color = CapColor,
                };

                // registered last so it draws over the track
                _completeLabel = new Label(this)
                {
                    Format = new GlyphFormat(FullCapColor, TextAlignment.Center, 0.62f),
                    Offset = Vector2.Zero,
                    Visible = false,
                };
            }

            public void SetProgress(double progress, bool healthStyle, string completeText)
            {
                var t = (float)MathHelperD.Clamp(progress, 0, 1);
                var innerWidth = BarWidth - FillInset * 2;
                var fillWidth = innerWidth * t;

                // health style drains, so "full" is the untouched state
                var complete = healthStyle ? t <= 0f : t >= 1f;

                if (healthStyle)
                {
                    _outline.Color = complete ? FullOutlineColor : HealthOutlineColor;
                    _fill.Color = HealthFillColor;
                    _cap.Color = HealthCapColor;
                }
                else
                {
                    _outline.Color = complete ? FullOutlineColor : OutlineColor;
                    _fill.Color = complete ? FullFillColor : FillColor;
                    _cap.Color = complete ? FullCapColor : CapColor;
                }

                var showStamp = complete && !string.IsNullOrEmpty(completeText);
                _completeLabel.Visible = showStamp;
                if (showStamp && completeText != _completeCache)
                {
                    _completeCache = completeText;
                    _completeLabel.Text = completeText;
                }

                _fill.Visible = fillWidth >= 1;
                _fill.Width = fillWidth < 1 ? 1 : fillWidth;
                _fill.Offset = new Vector2(FillInset, 0);

                _cap.Visible = fillWidth >= CapWidth;
                _cap.Offset = new Vector2(FillInset + fillWidth - CapWidth, 0);
            }
        }

        sealed class DotRail : HudElementBase
        {
            const int DotCount = 36;
            const float DotSize = 2;

            static readonly Color DotColor = new Color(187, 233, 246, 110);

            public DotRail(HudParentBase parent) : base(parent)
            {
                Size = new Vector2(CapsuleBar.BarWidth, DotSize);

                var step = (CapsuleBar.BarWidth - DotSize) / (DotCount - 1);
                for (var i = 0; i < DotCount; i++)
                {
                    // ReSharper disable once ObjectCreationAsStatement
                    new TexturedBox(this)
                    {
                        ParentAlignment = ParentAlignments.Left | ParentAlignments.InnerH,
                        Size = new Vector2(DotSize, DotSize),
                        Offset = new Vector2(i * step, 0),
                        Color = DotColor,
                    };
                }
            }
        }

    }
}
