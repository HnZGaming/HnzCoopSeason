using System;
using RichHudFramework.UI;
using VRageMath;

namespace HnzCoopSeason.Missions.HudElements
{
    public sealed class MissionListElement : BorderBox
    {
        readonly Label _typeLabel;
        readonly Label _progressLabel;
        readonly MouseInputElement _mouseInputElement;

        public long MissionId { get; private set; }
        public event Action<long> OnClicked;

        public MissionListElement(Vector2 size) : base(null)
        {
            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Top | ParentAlignments.Left;
            Size = size;
            Color = Color.Transparent;

            _typeLabel = new Label(this)
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left,
                Offset = new Vector2(12, 0),
                Text = "Acquisition",
            };

            _progressLabel = new Label(this)
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Right,
                Offset = new Vector2(-12, 0),
                Text = "[0%]",
            };

            _mouseInputElement = new MouseInputElement(this);
            _mouseInputElement.LeftClicked += OnMissionListElementClicked;
        }

        public override bool Unregister()
        {
            OnClicked = null;
            _mouseInputElement.LeftClicked -= OnMissionListElementClicked;
            return base.Unregister();
        }

        public void SetMission(Mission mission)
        {
            MissionId = mission.Id;

            _typeLabel.Text = $"{mission.Type}";

            _progressLabel.Text = $"[ {mission.ProgressPercentage:0}% ]";
        }

        void OnMissionListElementClicked(object sender, EventArgs e)
        {
            OnClicked?.Invoke(MissionId);
        }

        public void SetSelected(bool selected)
        {
            Color = selected ? Color.White : Color.Transparent;
        }
    }
}