using RichHudFramework.UI;
using VRageMath;

namespace HnzCoopSeason.Missions.HudElements
{
    public sealed class MissionListElement : HudChain
    {
        readonly Label _typeLabel;
        readonly Label _progressLabel;

        public MissionListElement(HudParentBase parent) : base(false, parent)
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
                ParentAlignment = ParentAlignments.Center | ParentAlignments.Inner | ParentAlignments.Left,
                Size = new Vector2(50, 20),
                Text = "1/300",
            };

            Add(_typeLabel);
            Add(_progressLabel);
        }

        public void SetMission(Mission mission)
        {
            _typeLabel.Text = mission.Type.ToString();
            _progressLabel.Text = $"{mission.Progress}/{mission.TotalProgress}";
        }
    }
}