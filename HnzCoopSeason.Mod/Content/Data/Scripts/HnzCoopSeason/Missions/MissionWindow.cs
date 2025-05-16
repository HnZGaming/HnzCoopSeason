using System;
using System.Linq;
using HnzCoopSeason.Missions.HudElements;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using Sandbox.ModAPI;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Missions
{
    /// <summary>
    ///     Example Text Editor window
    /// </summary>
    public class MissionWindow : WindowBase
    {
        static readonly Vector2 _missionElementSize = new Vector2(200, 30);
        static readonly Vector2 _windowBodySize = new Vector2(700, 270);
        static readonly float _windowHeaderHeight = 30;

        readonly IBindGroup _keyBinds;
        readonly ScrollBox<ScrollBoxEntry<MissionListElement>, MissionListElement> _missionList;
        readonly HudChain _detailPane;
        readonly Label _titleLabel;
        readonly Label _descriptionLabel;
        readonly SubmitButtonElement _submitLayout;

        public static MissionWindow Instance { get; private set; }

        public static void Load()
        {
            MyLog.Default.Info("[HnzCoopSeason] contract window load");
            try
            {
                Instance = new MissionWindow(HudMain.HighDpiRoot)
                {
                    Visible = true,
                    Size = new Vector2(_windowBodySize.X, _windowBodySize.Y + _windowHeaderHeight),
                    BodyColor = new Color(41, 54, 62, 150),
                    BorderColor = new Color(58, 68, 77),
                };
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] contract window load failed: {e}");
                return;
            }

            MyLog.Default.Info("[HnzCoopSeason] contract window load done");

            MissionService.Instance.OnMissionsUpdated += Instance.OnMissionsUpdated;
        }

        MissionWindow(HudParentBase parent = null) : base(parent)
        {
            header.Text = "Contracts -- open/close this window with tilda [~] key";
            header.Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.08f);
            header.Height = _windowHeaderHeight;
            body.Size = _windowBodySize;

            _keyBinds = BindManager.GetOrCreateGroup("CoopContractBinds");
            _keyBinds.RegisterBinds(new BindGroupInitializer
            {
                { "CoopContractToggle", MyKeys.OemTilde }
            });

            _keyBinds[0].NewPressed += ToggleWindow;

            _missionList = new ScrollBox<ScrollBoxEntry<MissionListElement>, MissionListElement>(true, bodyBg)
            {
                SizingMode = HudChainSizingModes.FitMembersOffAxis,
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Top | ParentAlignments.Left,
                Size = new Vector2(_missionElementSize.X, _windowBodySize.Y),
                Padding = new Vector2(6, 6),
            };

            var scrollBarWidth = _missionList.ScrollBar.Width + _missionList.Divider.Width + 20;

            _detailPane = new HudChain(true, bodyBg)
            {
                SizingMode = HudChainSizingModes.FitMembersOffAxis,
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Top | ParentAlignments.Right,
                Size = new Vector2(_windowBodySize.X - _missionElementSize.X - scrollBarWidth, _windowBodySize.Y),
                Padding = new Vector2(12, 12),
                Spacing = 12,
            };

            _titleLabel = new Label
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left,
                BuilderMode = TextBuilderModes.Wrapped,
                Format = new GlyphFormat(GlyphFormat.White.Color, TextAlignment.Left, 1, FontStyles.Underline),
                Text = "Acquisition",
            };

            _descriptionLabel = new Label
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left,
                BuilderMode = TextBuilderModes.Wrapped,
                Text = "Acquisition",
            };

            _submitLayout = new SubmitButtonElement(bodyBg)
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Bottom | ParentAlignments.Right,
            };

            _submitLayout.Initialize();

            _detailPane.Add(_titleLabel);
            _detailPane.Add(_descriptionLabel);
            _detailPane.Visible = false;
        }

        public void Unload()
        {
            MyLog.Default.Info("[HnzCoopSeason] contract window unload");
            _keyBinds.ClearSubscribers();

            MissionService.Instance.OnMissionsUpdated -= OnMissionsUpdated;
        }

        void ToggleWindow(object sender, EventArgs args)
        {
            SetVisible(!Visible);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            HudMain.EnableCursor = visible;
        }

        void OnMissionsUpdated(Mission[] missions)
        {
            _missionList.Clear();

            foreach (var mission in missions)
            {
                var element = new MissionListElement(_missionElementSize);
                element.SetMission(mission);
                element.OnSelected += OnMissionListElementClicked;
                _missionList.Add(element);
            }
        }

        void OnMissionListElementClicked(MissionListElement element)
        {
            var mission = element.Mission;
            MyLog.Default.Info($"[HnzCoopSeason] mission selected: {mission.Title}");

            _detailPane.Visible = true;
            _titleLabel.Text = mission.Title;
            _descriptionLabel.Text = mission.Description;

            foreach (var e in _missionList.CollectionContainer.Select(e => e.Element))
            {
                if (e.Mission != mission)
                {
                    e.Deselect();
                }
            }
        }

        public void Update()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 10 != 0) return;
            if (!Visible) return;

            var canSubmit = MissionService.CanSubmit();
            _submitLayout.SetInputEnabled(canSubmit);
        }
    }
}