using System;
using RichHudFramework.UI;
using VRageMath;

namespace HnzCoopSeason.Missions.Hud
{
    public sealed class SubmitButtonElement : HudChain
    {
        readonly BorderBox _buttonBorder;
        readonly LabelButton _button;
        readonly Label _noteLabel;

        public event Action OnSubmit;

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
            };

            _noteLabel = new Label
            {
                ParentAlignment = ParentAlignments.Inner,
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

        public void SetEnabled(bool enabled, string note)
        {
            var buttonColor = enabled ? Color.White : Color.Gray;
            _button.InputEnabled = enabled;
            _button.Text = new RichText("Submit", new GlyphFormat(buttonColor));
            _buttonBorder.Color = buttonColor;

            var labelColor = enabled ? Color.White : Color.Red;
            _noteLabel.Text = new RichText(note, new GlyphFormat(labelColor));
        }

        void OnLeftClicked(object sender, EventArgs e)
        {
            OnSubmit?.Invoke();
        }
    }
}