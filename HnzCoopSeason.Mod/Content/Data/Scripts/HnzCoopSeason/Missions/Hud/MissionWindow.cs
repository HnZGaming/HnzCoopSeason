using System;
using System.Linq;
using HnzCoopSeason.HudUtils;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Missions.Hud
{
    /// <summary>
    ///     Example Text Editor window
    /// </summary>
    public class MissionWindow : WindowBase
    {
        const float WindowHeaderHeight = 30;
        static readonly Vector2 _missionElementSize = new Vector2(200, 30);
        static readonly Vector2 _windowBodySize = new Vector2(700, 270);

        readonly IBindGroup _keyBinds;
        readonly ScrollBox<ScrollBoxEntry<MissionListElement>, MissionListElement> _missionList;
        readonly HudChain _detailPane;
        readonly Label _titleLabel;
        readonly Label _descriptionLabel;
        readonly Label _statusLabel;
        readonly SubmitButtonElement _submitLayout;
        HudDisplayMode _displayMode;

        public static MissionWindow Instance { get; private set; }

        public static void Load()
        {
            MyLog.Default.Info("[HnzCoopSeason] mission window load");
            try
            {
                Instance = new MissionWindow(HudMain.HighDpiRoot)
                {
                    Visible = true,
                    Size = new Vector2(_windowBodySize.X, _windowBodySize.Y + WindowHeaderHeight),
                    BodyColor = new Color(41, 54, 62, 240),
                    BorderColor = new Color(58, 68, 77),
                    AllowResizing = false,
                };

                Instance.SetDisplayMode(HudDisplayMode.Hidden);
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] mission window load failed: {e}");
                return;
            }

            MyLog.Default.Info("[HnzCoopSeason] mission window load done");
        }

        MissionWindow(HudParentBase parent = null) : base(parent)
        {
            MyLog.Default.Info("[HnzCoopSeason] mission window ctor");

            header.Text = "Missions -- switch display mode with tilda (~) key";
            header.Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.08f);
            header.Height = WindowHeaderHeight;
            body.Size = _windowBodySize;

            _keyBinds = BindManager.GetOrCreateGroup("CoopMissionsBinds");
            _keyBinds.RegisterBinds(new BindGroupInitializer
            {
                { "CoopMissionsToggle", MyKeys.OemTilde },
                { "Escape", MyKeys.Escape },
            });

            _keyBinds[0].NewPressed += OnTildaKeyPressed;
            _keyBinds[1].NewPressed += OnEscapeKeyPressed;

            _missionList = new ScrollBox<ScrollBoxEntry<MissionListElement>, MissionListElement>(true, bodyBg)
            {
                SizingMode = HudChainSizingModes.FitMembersOffAxis,
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Top | ParentAlignments.Left,
                Size = new Vector2(_missionElementSize.X, _windowBodySize.Y),
                Padding = new Vector2(6, 6),
                Color = Color.Transparent,
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
                Text = "--",
            };

            _descriptionLabel = new Label
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left,
                BuilderMode = TextBuilderModes.Wrapped,
                Text = "--",
            };

            _statusLabel = new Label
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left,
                BuilderMode = TextBuilderModes.Wrapped,
                Text = "--",
            };

            _submitLayout = new SubmitButtonElement(body)
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Bottom | ParentAlignments.Right,
            };

            _submitLayout.Initialize();
            _submitLayout.OnSubmit += OnSubmitButtonPressed;

            _detailPane.Add(_titleLabel);
            _detailPane.Add(_descriptionLabel);
            _detailPane.Add(_statusLabel);
            _detailPane.Visible = false;

            MissionService.Instance.OnMissionsReceived += OnMissionsReceived;
            MissionService.Instance.OnMissionSelected += OnMissionSelected;
            MissionService.Instance.OnClientSubmitEnabledChanged += OnSubmitEnabledChanged;
            MissionService.Instance.OnClientMissionStatusChanged += OnMissionStatusChanged;

            MyLog.Default.Info("[HnzCoopSeason] mission window ctor done");
        }

        public void Unload()
        {
            MyLog.Default.Info("[HnzCoopSeason] mission window unload");
            _keyBinds.ClearSubscribers();
            MissionService.Instance.OnMissionsReceived -= OnMissionsReceived;
            MissionService.Instance.OnMissionSelected -= OnMissionSelected;
            MissionService.Instance.OnClientSubmitEnabledChanged -= OnSubmitEnabledChanged;
            MissionService.Instance.OnClientMissionStatusChanged -= OnMissionStatusChanged;
        }

        void OnTildaKeyPressed(object sender, EventArgs args)
        {
            SetDisplayMode(_displayMode.Increment());
        }

        void OnEscapeKeyPressed(object sender, EventArgs e)
        {
            SetDisplayMode(HudDisplayMode.Hidden);
        }

        public void SetDisplayMode(HudDisplayMode displayMode)
        {
            MyLog.Default.Info($"[HnzCoopSeason] mission window display mode: {displayMode}");

            _displayMode = displayMode;
            ((WindowBase)this).SetDisplayMode(displayMode);

            if (Visible)
            {
                MissionService.Instance.RequestUpdate();
            }
        }

        public void Update()
        {
            if (!Visible) return;

            MissionService.Instance.UpdateClient();
        }

        void OnMissionsReceived(Mission[] missions)
        {
            _missionList.Clear();

            for (var i = 0; i < missions.Length; i++)
            {
                var mission = missions[i];
                var isCurrentMission = i == MissionService.Instance.CurrentMissionIndex;
                var element = new MissionListElement(_missionElementSize, mission, isCurrentMission);
                element.OnClicked += OnMissionListElementClicked;
                _missionList.Add(element);
            }
        }

        void OnMissionListElementClicked(int missionIndex)
        {
            MissionService.Instance.SelectMission(missionIndex);
        }

        void OnMissionSelected(Mission mission) // or null
        {
            _detailPane.Visible = true;
            _titleLabel.Text = mission?.Title;
            _descriptionLabel.Text = mission?.Description;
            _statusLabel.Text = ""; //todo faulty

            foreach (var e in _missionList.CollectionContainer.Select(e => e.Element))
            {
                e.SetSelected(e.MissionIndex == mission?.Index);
            }
        }

        void OnSubmitEnabledChanged(bool enable, string note)
        {
            _submitLayout.SetEnabled(enable, note);
        }

        void OnSubmitButtonPressed()
        {
            MissionService.Instance.SendSubmissionToServer();
        }

        void OnMissionStatusChanged(string status)
        {
            _statusLabel.Text = status.Trim();
        }
    }
}