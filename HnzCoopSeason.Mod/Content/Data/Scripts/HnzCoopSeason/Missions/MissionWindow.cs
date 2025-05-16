using System;
using System.Linq;
using HnzCoopSeason.Missions.HudElements;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
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
        readonly IBindGroup _keyBinds;
        readonly ScrollBox<ScrollBoxEntry<MissionListElement>, MissionListElement> _missionList;
        readonly HudChain _detailPane;
        readonly Label _titleLabel;
        readonly Label _descriptionLabel;
        readonly LabelBoxButton _submitButton;

        public static MissionWindow Instance { get; private set; }

        public static void Load()
        {
            MyLog.Default.Info("[HnzCoopSeason] contract window load");
            try
            {
                Instance = new MissionWindow(HudMain.HighDpiRoot)
                {
                    Visible = true,
                    HeaderText = "Contracts (open/close with tilda [~] key)",
                    Size = new Vector2(500f, 300f),
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
            header.Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.08f);
            header.Height = 30;
            body.Size = new Vector2(500, 270);

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
                Offset = Vector2.Zero,
                Size = new Vector2(150, 270),
                Padding = new Vector2(6, 6),
            };

            _detailPane = new HudChain(true, bodyBg)
            {
                SizingMode = HudChainSizingModes.FitMembersOffAxis,
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Top | ParentAlignments.Right,
                Offset = Vector2.Zero,
                Size = new Vector2(270, 270),
                Padding = new Vector2(6, 6),
                Spacing = 12,
            };

            _titleLabel = new Label
            {
                ParentAlignment = ParentAlignments.Center | ParentAlignments.Inner | ParentAlignments.Left,
                Size = new Vector2(250, 20),
                Text = "Acquisition",
            };

            _descriptionLabel = new Label
            {
                ParentAlignment = ParentAlignments.Center | ParentAlignments.Inner | ParentAlignments.Left,
                Size = new Vector2(250, 60),
                Text = "Acquisition",
            };

            _submitButton = new LabelBoxButton
            {
                ParentAlignment = ParentAlignments.Center | ParentAlignments.Inner | ParentAlignments.Left,
                Size = new Vector2(200, 20),
                Text = "Submit",
            };

            _detailPane.Add(_titleLabel);
            _detailPane.Add(_descriptionLabel);
            _detailPane.Add(_submitButton);
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
                var element = new MissionListElement();
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
    }
}