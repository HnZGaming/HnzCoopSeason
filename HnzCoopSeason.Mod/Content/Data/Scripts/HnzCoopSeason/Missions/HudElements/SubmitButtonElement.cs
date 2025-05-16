using System;
using RichHudFramework.UI;
using VRageMath;

namespace HnzCoopSeason.Missions.HudElements
{
    public sealed class SubmitButtonElement : HudChain
    {
        readonly BorderBox _buttonBorder;
        readonly LabelButton _button;
        readonly Label _noteLabel;

        public event Action OnSubmit;

        public string SubmitNote
        {
            set { _noteLabel.Text = value; }
        }

        public SubmitButtonElement(HudParentBase parent = null) : base(false, parent)
        {
            SizingMode = HudChainSizingModes.FitChainBoth;
            Padding = new Vector2(24, 24);
            Spacing = 12;

            _buttonBorder = new BorderBox
            {
                Size = new Vector2(100, 30),
                Color = Color.White,
            };

            _button = new LabelButton(_buttonBorder)
            {
                ParentAlignment = ParentAlignments.Inner,
                Padding = new Vector2(6, 6),
                Text = "Submit",
            };

            _noteLabel = new Label
            {
                ParentAlignment = ParentAlignments.Inner,
                Text = "You must find the contract block",
            };

            _button.MouseInput.LeftClicked += OnLeftClicked;
        }

        public void Initialize()
        {
            Add(_noteLabel);
            Add(_buttonBorder);
        }

        public override bool Unregister()
        {
            OnSubmit = null;
            _button.MouseInput.LeftClicked -= OnLeftClicked;
            return base.Unregister();
        }

        public void SetEnabled(bool enabled)
        {
            _button.InputEnabled = enabled;

            var color = enabled ? Color.White : Color.Gray;
            _button.Format = new GlyphFormat(color);
            _buttonBorder.Color = color;
        }

        void OnLeftClicked(object sender, EventArgs e)
        {
            OnSubmit?.Invoke();
        }
    }
}