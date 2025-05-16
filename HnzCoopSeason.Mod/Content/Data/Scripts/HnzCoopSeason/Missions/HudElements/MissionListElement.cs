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
                Text = "1/300",
            };

            _mouseInputElement = new MouseInputElement(this);
            _mouseInputElement.LeftClicked += OnMissionListElementClicked;
        }

        public Mission Mission { get; private set; }
        public event Action<MissionListElement> OnSelected;

        public override bool Unregister()
        {
            OnSelected = null;
            _mouseInputElement.LeftClicked -= OnMissionListElementClicked;
            return base.Unregister();
        }

        public void SetMission(Mission mission)
        {
            Mission = mission;

            _typeLabel.Text = $"{mission.Type}";

            var progressPercentage = (mission.Progress / mission.TotalProgress) * 100;
            _progressLabel.Text = $"{progressPercentage:0}%";
        }

        void OnMissionListElementClicked(object sender, EventArgs e)
        {
            Color = Color.White;
            OnSelected?.Invoke(this);
        }

        public void Deselect()
        {
            Color = Color.Transparent;
        }
    }
}