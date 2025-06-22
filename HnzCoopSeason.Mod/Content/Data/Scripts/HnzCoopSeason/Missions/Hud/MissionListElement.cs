using System;
using RichHudFramework.UI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Missions.Hud
{
    public sealed class MissionListElement : BorderBox
    {
        readonly Label _typeLabel;
        readonly Label _progressLabel;
        readonly MouseInputElement _mouseInputElement;

        public int MissionIndex { get; private set; }
        public event Action<int> OnClicked;

        public MissionListElement(Vector2 size, Mission mission, bool isCurrentMission) : base(null)
        {
            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Top | ParentAlignments.Left;
            Size = size;
            Color = Color.Transparent;
            MissionIndex = mission.Index;
            var textColor = isCurrentMission ? Color.White : Color.Gray;

            _typeLabel = new Label(this)
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left,
                Offset = new Vector2(12, 0),
                Text = new RichText($"{mission.Type}", new GlyphFormat(textColor)),
            };

            _progressLabel = new Label(this)
            {
                ParentAlignment = ParentAlignments.Inner | ParentAlignments.Right,
                Offset = new Vector2(-12, 0),
                Text = new RichText($"[ {mission.ProgressPercentage:0}% ]", new GlyphFormat(textColor)),
            };

            MyLog.Default.Info($"[HnzCoopSeason] mission list element [{MissionIndex}] init; progress: {mission.Progress}, goal: {mission.Goal} percentage: {mission.ProgressPercentage}");

            _mouseInputElement = new MouseInputElement(this);
            _mouseInputElement.LeftClicked += OnMissionListElementClicked;
        }

        public override bool Unregister()
        {
            OnClicked = null;
            _mouseInputElement.LeftClicked -= OnMissionListElementClicked;
            return base.Unregister();
        }

        void OnMissionListElementClicked(object sender, EventArgs e)
        {
            OnClicked?.Invoke(MissionIndex);
        }

        public void SetSelected(bool selected)
        {
            Color = selected ? Color.White : Color.Transparent;
        }
    }
}