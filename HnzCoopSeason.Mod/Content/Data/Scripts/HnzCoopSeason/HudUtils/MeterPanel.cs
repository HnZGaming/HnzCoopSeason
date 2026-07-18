using System;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using VRageMath;

namespace HnzCoopSeason.HudUtils
{
    /// <summary>
    ///     Screen-top meter panel; sci-fi interface styling (outlined capsule bar with
    ///     inset fill, dotted under-rail, corner frame brackets) in the vanilla HUD palette.
    ///     Layout:
    ///         Title
    ///         [============------------------]   outlined track, inset fill
    ///         . . . . . . . . . . . . . . . .    dotted rail
    ///         PEACEMETER                42 %     label / value row
    ///         (subtitle / description)
    ///     Reads the winning MeterState from ScreenTopHud every frame.
    /// </summary>
    public sealed class MeterPanel : HudElementBase
    {
        const float LineSpacing = 7;
        const float BackPaddingX = 18;
        const float BackPaddingY = 14;
        const float BottomTrimPadding = 17; // main body's trimmed bottom edge sits this far under the last line
        const float InfoIconSize = 17;
        const float InfoIconGap = 7;
        const float InfoIconNudge = 0; // badge sits on the title's centre line
        const float MinimalRailPadding = 5; // extra room under the dotted rail when it is the last row
        const float TitleValueGap = 12; // minimum clear space between the title and the % readout

        // vanilla HUD design system: one pale-cyan accent (0.734, 0.914, 0.965) on a dark plate
        internal static readonly Color VanillaCyan = new Color(187, 233, 246);
        // vanilla stat plate (RightPlate.png = 41,54,62) x HUD background opacity as alpha.
        // RGB dropped slightly below the texture value: alpha governs the bright-background look
        // (correct as-is), RGB governs the dark-background look (41,54,62 read a touch too bright).
        internal static readonly Color BackColor = new Color(33, 44, 51);
        static readonly Color TitleColor = VanillaCyan;
        static readonly Color DescriptionColor = new Color(187, 233, 246, 150); // accent at ~60%
        static readonly Color SubtitleColor = DescriptionColor; // body lines share one colour so the two meters read alike

        public float TopMargin = 50; // px down from the top edge of the screen (1080p-normalized); F2 slider
        public MeterAnchor Anchor = MeterAnchor.Left; // which horizontal edge to pin to; F2 setting
        public bool Minimal; // F2 setting: title + bar only, no subtitle/description lines
        const float SideMargin = 30; // px in from the left/right screen edge when side-anchored (1080p-normalized)

        readonly ChamferedPlate _background;
        readonly InfoIcon _infoIcon;
        readonly Label _titleLabel;
        readonly Label _valueLabel;
        readonly CapsuleBar _bar;
        readonly DotRail _dotRail;
        readonly Label _nameLabel;
        readonly Label _subtitleLabel;
        readonly Label _descriptionLabel;

        float _plateHeight = 120; // last laid-out plate height; seeds PlateBottomLeft before the meter first draws
        string _titleSource; // raw title the fitted label text was derived from
        float _titleBudget;
        string _valueCache, _nameCache, _subtitleCache, _descriptionCache; // last text pushed to each label

        public MeterPanel(HudParentBase parent) : base(parent)
        {
            // roots are size-less anchors at screen center; this panel is a zero-size
            // anchor point placed near the top edge in Layout(), children stack down from it
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
            // hide whenever the vanilla HUD is hidden:
            // - Config.MinimalHud covers both the backslash toggle (hud off) and Tab minimal state
            // - Gui.IsCursorVisible covers ESC/pause menu, terminal, inventory, and any other screen
            var config = Sandbox.ModAPI.MyAPIGateway.Session.Config;
            var hudVisible = !config.MinimalHud && !Sandbox.ModAPI.MyAPIGateway.Gui.IsCursorVisible;
            var state = hudVisible ? ScreenTopHud.Instance.Current : null;
            SetContentVisible(state != null);
            if (state == null) return;

            // plate translucency follows the game's HUD background opacity setting
            var opacity = MathHelper.Clamp(config.HUDBkOpacity, 0f, 1f);
            _background.SetColor(new Color(BackColor.R, BackColor.G, BackColor.B, (byte)(opacity * 255)));

            // pin the anchor near the top edge, on the configured side (HighDpiRoot space)
            Offset = new Vector2(
                AnchorOffsetX(),
                HudMain.ScreenHeight / HudMain.ResScale * 0.5f - TopMargin);

            _bar.SetProgress(state.Progress);

            const float halfWidth = CapsuleBar.BarWidth / 2;

            // stack visible lines downward from the anchor.
            // top row: title left-aligned, value readout right-aligned
            var y = 0f;
            // value readout first: whatever it leaves is the title's width budget
            _valueLabel.Visible = true;
            SetTextIfChanged(_valueLabel, state.ValueText ?? "", ref _valueCache);

            // optional alert badge sits at the left edge; the title starts after it
            _infoIcon.Visible = state.ShowInfoIcon;
            var titleLeft = -halfWidth + (state.ShowInfoIcon ? InfoIconSize + InfoIconGap : 0);

            _titleLabel.Visible = true;
            ApplyTitle(state.Title ?? "", halfWidth - _valueLabel.Width - TitleValueGap - titleLeft);
            _titleLabel.Offset = new Vector2(titleLeft + _titleLabel.Width / 2, y - _titleLabel.Height / 2);

            if (state.ShowInfoIcon)
            {
                // align to the title's centre line, not the row top, or the badge floats high
                _infoIcon.Offset = new Vector2(-halfWidth + InfoIconSize / 2, _titleLabel.Offset.Y - InfoIconNudge);
            }

            _valueLabel.Offset = new Vector2(halfWidth - _valueLabel.Width / 2, y - _valueLabel.Height / 2);

            y -= MathHelper.Max(_titleLabel.Height, _valueLabel.Height) + LineSpacing;

            _bar.Offset = new Vector2(0, y - _bar.Height / 2);
            y -= _bar.Height + 5;

            _dotRail.Offset = new Vector2(0, y - _dotRail.Height / 2 - 3); // drawn a touch lower; stacking unchanged
            y -= _dotRail.Height + 5;

            // minimal mode keeps the title row, bar and rail; the prose lines drop out
            y = Stack(_subtitleLabel, Minimal ? "" : state.Subtitle, y, ref _subtitleCache);
            y = Stack(_descriptionLabel, Minimal ? "" : state.Description, y, ref _descriptionCache);

            // main plate body covers title through rail (+extra lines when present).
            // the -LineSpacing assumes a text line closed the stack; in minimal mode the rail is
            // last, so give the dots their own breathing room instead of trimming to them
            var contentHeight = -y - LineSpacing + (Minimal ? MinimalRailPadding : 0);
            var mainSize = new Vector2(CapsuleBar.BarWidth + BackPaddingX * 2, contentHeight + BackPaddingY + BottomTrimPadding);
            var mainBottom = -(contentHeight + BottomTrimPadding);

            // meter name lives on the bottom-left tab below the main body
            _nameLabel.Visible = true;
            SetTextIfChanged(_nameLabel, state.BarName ?? "", ref _nameCache);
            // tab band = exactly one chamfer tall: its edges run straight into the 45-degree
            // cuts with no vertical segment; the label rides up into the bottom padding
            const float tabHeight = 14;
            var tabWidth = BackPaddingX + _nameLabel.Width + 26; // label + right slack
            _nameLabel.Offset = new Vector2(-halfWidth + _nameLabel.Width / 2, mainBottom + 2);

            _background.SetPlateSize(mainSize, tabWidth, tabHeight, Minimal);
            _background.Offset = new Vector2(0, BackPaddingY - (mainSize.Y + tabHeight) / 2);
            _plateHeight = mainSize.Y + tabHeight;
        }

        /// <summary>
        ///     Bottom-left corner of the drawn plate in HighDpiRoot space — the settings
        ///     window parks itself under this. Valid even while the meter is hidden
        ///     (anchor math is recomputed; only the height is remembered from the last draw).
        /// </summary>
        public Vector2 PlateBottomLeft => new Vector2(
            AnchorOffsetX() - (CapsuleBar.BarWidth / 2 + BackPaddingX),
            HudMain.ScreenHeight / HudMain.ResScale * 0.5f - TopMargin + BackPaddingY - _plateHeight);

        /// <summary>
        ///     X of the anchor point for the configured side. Children are laid out
        ///     symmetrically about the anchor, so a side anchor shifts inward by the
        ///     panel's own half width to land its outer edge at SideMargin.
        /// </summary>
        float AnchorOffsetX()
        {
            if (Anchor == MeterAnchor.Center) return 0;

            const float panelHalfWidth = CapsuleBar.BarWidth / 2 + BackPaddingX;
            var inset = HudMain.ScreenWidth / HudMain.ResScale * 0.5f - SideMargin - panelHalfWidth;
            return Anchor == MeterAnchor.Left ? -inset : inset;
        }

        /// <summary>
        ///     Alert badge — the vanilla LCD "Danger" sprite (orange warning triangle), declared
        ///     as CoopHudAlert in TransparentMaterials.sbc. Drawn white so the texture keeps its
        ///     own colours; a ring-plus-glyph drawn at this size just reads as mush.
        /// </summary>
        sealed class InfoIcon : HudElementBase
        {
            static readonly Material AlertMat = new Material("CoopHudAlert", new Vector2(512));

            public InfoIcon(HudParentBase parent, float size) : base(parent)
            {
                Size = new Vector2(size);

                // ReSharper disable once ObjectCreationAsStatement — parents itself
                new TexturedBox(this)
                {
                    DimAlignment = DimAlignments.Both,
                    Color = Color.White,
                    Material = AlertMat,
                };
            }
        }

        /// <summary>
        ///     Assigns label text only when it actually changed. RichHud's SetText has no equality
        ///     check — the Master does `formatter.Clear(); formatter.Append(text)`, re-parsing every
        ///     glyph — so writing unchanged text each frame re-lays-out the whole string 60x/sec.
        ///     Build Vision throttles the same cost behind a tick counter (TextTickDivider); an
        ///     exact compare is better still, since these strings change roughly once a second.
        /// </summary>
        static void SetTextIfChanged(Label label, string text, ref string cache)
        {
            if (text == cache) return;

            cache = text;
            label.Text = text;
        }

        /// <summary>
        ///     Sets the title, truncating with an ellipsis if it would run past the % readout —
        ///     NPC grid names are arbitrary player-authored strings and the label auto-sizes to
        ///     its text, so an unclamped one overruns the plate entirely.
        ///     Measures by assigning and reading Width back (the framework has no measure-only
        ///     call), so the result is cached: rebuilding a text board every frame is not free,
        ///     and the title only changes when the target does.
        /// </summary>
        void ApplyTitle(string text, float budget)
        {
            if (text == _titleSource && Math.Abs(budget - _titleBudget) < 0.5f) return;

            _titleSource = text;
            _titleBudget = budget;

            _titleLabel.Text = text;
            if (budget <= 0 || _titleLabel.Width <= budget) return;

            // proportional first guess from the measured overflow, then shave until it fits
            var keep = Math.Max(1, (int)(text.Length * budget / _titleLabel.Width) - 1);
            while (keep > 1)
            {
                _titleLabel.Text = text.Substring(0, keep).TrimEnd() + "...";
                if (_titleLabel.Width <= budget) return;

                keep--;
            }
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

        float Stack(Label label, string text, float y, ref string cache)
        {
            if (string.IsNullOrEmpty(text))
            {
                label.Visible = false;
                return y;
            }

            label.Visible = true;
            SetTextIfChanged(label, text, ref cache);

            // Width is only correct once Text is assigned, so the flush-left x is computed here
            // rather than passed in — a caller-side value would lag a frame behind text changes
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
            const float Chamfer = 14; // logical px (1080p-normalized)
            // deeper cut opposite the name tab, detailed mode only — in minimal the plate is short
            // enough that an oversized cut would eat most of its right edge, so it goes back to
            // matching the other three corners
            const float BottomRightChamfer = Chamfer * 2.25f;
            const float AtlasSize = 64;
            const float CellSize = 32;

            readonly TexturedBox _topStrip;
            readonly TexturedBox _middle;
            readonly TexturedBox _mainBottomStrip; // main-body bottom band, right of the tab
            readonly TexturedBox _tabStrip; // label tab band, bottom-left
            readonly TexturedBox _tabLeftFill; // tab left column above its corner cut
            readonly TexturedBox _tabRightFill; // tab right column above its corner cut
            readonly TexturedBox[] _corners; // TL, TR, main-BR, tab-BL, tab ramp (BR-cell)
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
                    new Vector2(0, 0), // TL
                    new Vector2(CellSize, 0), // TR
                    new Vector2(CellSize, CellSize), // main bottom-right
                    new Vector2(0, CellSize), // tab bottom-left
                    new Vector2(CellSize, CellSize), // tab ramp (bottom-right shape)
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

            /// <param name="mainSize">main body size (title through dotted rail, padded)</param>
            /// <param name="tabWidth">width of the bottom-left label tab</param>
            /// <param name="tabHeight">height of the label tab band</param>
            /// <param name="minimal">true to square the bottom-right cut back to the others</param>
            public void SetPlateSize(Vector2 mainSize, float tabWidth, float tabHeight, bool minimal)
            {
                var bottomRight = minimal ? Chamfer : BottomRightChamfer;

                Size = new Vector2(mainSize.X, mainSize.Y + tabHeight);

                var left = -mainSize.X / 2;
                var top = mainSize.Y / 2 + tabHeight / 2; // element center accounts for the tab
                var mainBottom = top - mainSize.Y;

                // top chamfer band
                _topStrip.Size = new Vector2(mainSize.X - Chamfer * 2, Chamfer);
                _topStrip.Offset = new Vector2(0, top - Chamfer / 2);
                _corners[0].Size = new Vector2(Chamfer);
                _corners[0].Offset = new Vector2(left + Chamfer / 2, top - Chamfer / 2);
                _corners[1].Size = new Vector2(Chamfer);
                _corners[1].Offset = new Vector2(-left - Chamfer / 2, top - Chamfer / 2);

                // middle band down to the main bottom chamfer, which is the deeper one
                _middle.Size = new Vector2(mainSize.X, mainSize.Y - Chamfer - bottomRight);
                _middle.Offset = new Vector2(0, top - Chamfer - _middle.Height / 2);

                // main bottom band: full left (continues into the tab), chamfered right. Its height
                // follows the bottom-right cut; the left end stays square so no filler is needed
                // there, unlike the top band.
                _mainBottomStrip.Size = new Vector2(mainSize.X - bottomRight, bottomRight);
                _mainBottomStrip.Offset = new Vector2(left + _mainBottomStrip.Width / 2, mainBottom + bottomRight / 2);
                _corners[2].Size = new Vector2(bottomRight);
                _corners[2].Offset = new Vector2(-left - bottomRight / 2, mainBottom + bottomRight / 2);

                // label tab: equal 45-degree chamfers on BOTH bottom corners; its right edge is a
                // plain vertical step down from the main body
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

        /// <summary>
        ///     Outlined capsule track with an inset fill and a bright end block:
        ///     [ ███████████████▓----------------- ]
        /// </summary>
        sealed class CapsuleBar : HudElementBase
        {
            public const float BarWidth = 430;
            const float BarHeight = 20;
            const float OutlineThickness = 1.5f;
            const float FillInset = 6; // gap between outline and fill
            const float CapWidth = 4;

            static readonly Color OutlineColor = new Color(187, 233, 246, 220);
            static readonly Color FillColor = VanillaCyan;
            static readonly Color CapColor = new Color(240, 252, 255);

            // "complete" green, matched to the vanilla O2/stat ICON green (the vivid one), not the
            // muted status-BAR green (HUD_STATUS_BAR_COLOR_GREEN_STATUS = 124,174,125). The icons
            // are white textures tinted at runtime, so there is no constant to quote — this is
            // eyeballed off the icon and safe to nudge.
            static readonly Color FullOutlineColor = new Color(46, 204, 64, 220);
            static readonly Color FullFillColor = new Color(46, 204, 64);
            static readonly Color FullCapColor = new Color(150, 240, 165); // same hue, lifted so the end block still reads

            readonly BorderBox _outline;
            readonly TexturedBox _fill;
            readonly TexturedBox _cap;

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
            }

            public void SetProgress(double progress)
            {
                var t = (float)MathHelperD.Clamp(progress, 0, 1);
                var innerWidth = BarWidth - FillInset * 2;
                var fillWidth = innerWidth * t;

                // whole bar flips green on completion, outline included, so it reads as one state
                var full = t >= 1f;
                _outline.Color = full ? FullOutlineColor : OutlineColor;
                _fill.Color = full ? FullFillColor : FillColor;
                _cap.Color = full ? FullCapColor : CapColor;

                _fill.Visible = fillWidth >= 1;
                _fill.Width = fillWidth < 1 ? 1 : fillWidth;
                _fill.Offset = new Vector2(FillInset, 0);

                // bright block at the leading edge of the fill
                _cap.Visible = fillWidth >= CapWidth;
                _cap.Offset = new Vector2(FillInset + fillWidth - CapWidth, 0);
            }
        }

        /// <summary>
        ///     Decorative dotted rail:  . . . . . . . . . . . . . . .
        /// </summary>
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
