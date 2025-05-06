using System;
using RichHudFramework.UI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Missions.HudElements
{
    public sealed class MissionListElement : HudChain<HudElementContainer<Label>, Label>
    {
        readonly Label _typeLabel;
        readonly Label _progressLabel;
        readonly MouseInputElement _mouseInputElement;

        public MissionListElement(HudParentBase parent = null) : base(false, parent)
        {
            SizingMode = HudChainSizingModes.FitMembersOffAxis | HudChainSizingModes.FitChainOffAxis;
            ParentAlignment = ParentAlignments.Top | ParentAlignments.Inner | ParentAlignments.Left;
            Size = new Vector2(250, 30);
            Padding = new Vector2(6, 6);
            Spacing = 12;

            _typeLabel = new Label
            {
                ParentAlignment = ParentAlignments.Center | ParentAlignments.Inner | ParentAlignments.Left,
                Size = new Vector2(200, 20),
                Text = "Acquisition",
            };

            _progressLabel = new Label
            {
                ParentAlignment = ParentAlignments.Center | ParentAlignments.Inner | ParentAlignments.Right,
                Size = new Vector2(50, 20),
                Text = "1/300",
            };

            _mouseInputElement = new MouseInputElement(this);
            _mouseInputElement.LeftClicked += OnMissionListElementClicked;

            Add(_typeLabel);
            Add(_progressLabel);
        }

        public Mission Mission { get; private set; }

        public override bool Unregister()
        {
            _mouseInputElement.LeftClicked -= OnMissionListElementClicked;
            return base.Unregister();
        }

        public void SetMission(Mission mission)
        {
            Mission = mission;
            _typeLabel.Text = mission.Type.ToString();
            _progressLabel.Text = $"{mission.Progress}/{mission.TotalProgress}";
        }

        void OnMissionListElementClicked(object sender, EventArgs e)
        {
            MissionWindow.Instance.OnMissionListElementClicked(Mission);
        }
    }
}