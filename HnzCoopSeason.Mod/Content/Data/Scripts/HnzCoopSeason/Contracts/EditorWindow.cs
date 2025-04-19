using System;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using RichHudFramework.UI.Rendering;
using VRage.Input;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Contracts
{
    /// <summary>
    /// Example Text Editor window
    /// </summary>
    public partial class TextEditor : WindowBase
    {
        public static TextEditor Instance { get; private set; }

        private readonly EditorToolBar toolBar;
        private readonly EditorTextBox textBox;
        private IBindGroup editorBinds;

        public static void Load()
        {
            Instance = new TextEditor(HudMain.HighDpiRoot)
            {
                Visible = false, // I don't want this to be visible on init.
            };

            MyLog.Default.Info("[HnzCoopSeason] contract window load");
        }

        /// <summary>
        /// Initializes a new Text Editor window and registers it to the specified parent element.
        /// You can leave the parent null and use the parent element's register method if you prefer.
        /// </summary>
        TextEditor(HudParentBase parent = null) : base(parent)
        {
            editorBinds = BindManager.GetOrCreateGroup("editorBinds");
            editorBinds.RegisterBinds(new BindGroupInitializer()
            {
                { "editorToggle", MyKeys.Up }
            });

            editorBinds[0].NewPressed += ToggleEditor;

            textBox = new EditorTextBox(body)
            {
                ParentAlignment = ParentAlignments.Bottom | ParentAlignments.InnerV,
                DimAlignment = DimAlignments.Width
            };

            toolBar = new EditorToolBar(header)
            {
                DimAlignment = DimAlignments.Width,
                ParentAlignment = ParentAlignments.Bottom,
                Format = textBox.text.Format,
                BulderMode = textBox.text.BuilderMode,
            };

            toolBar.FormatChanged += ChangeFormat;
            toolBar.BuildModeChanged += ChangeBuilderMode;

            // Window styling:
            BodyColor = new Color(41, 54, 62, 150);
            BorderColor = new Color(58, 68, 77);

            header.Format = new GlyphFormat(GlyphFormat.Blueish.Color, TextAlignment.Center, 1.08f);
            header.Height = 30f;

            HeaderText = "Example Text Editor";
            Size = new Vector2(500f, 300f);
        }

        void ToggleEditor(object sender, EventArgs args)
        {
            Visible = !Visible;
            HudMain.EnableCursor = Visible;
        }

        /// <summary>
        /// Updates text box formatting in response to input from the toolbar
        /// </summary>
        private void ChangeFormat()
        {
            ITextBoard textBoard = textBox.text.TextBoard;
            textBoard.Format = toolBar.Format;

            // Apply new formatting to selected text range. This will completely overwrite the text 
            // formatting of the selected range. Modifying it to only apply the last effect/setting
            // used would require setting formatting per-character. I should probably add a function
            // for that in the future, probably using some kind of bit mask.
            if (!textBox.text.SelectionEmpty)
                textBoard.SetFormatting(textBox.text.SelectionStart, textBox.text.SelectionEnd, toolBar.Format);
        }

        /// <summary>
        /// Changes text box builder mode based on toolbar input
        /// </summary>
        private void ChangeBuilderMode(object sender, EventArgs args)
        {
            textBox.text.BuilderMode = toolBar.BulderMode;
        }

        protected override void Layout()
        {
            base.Layout();

            // Set window minimum width to prevent it from becoming narrower than the toolbar's minimum width
            MinimumSize = new Vector2(Math.Max(toolBar.MinimumWidth, MinimumSize.X), MinimumSize.Y);

            // Match text box height to body height less toolbar height
            textBox.Height = body.Height - toolBar.Height;
        }

        public void Unload()
        {
            MyLog.Default.Info("[HnzCoopSeason] contract window unload");
            editorBinds.ClearSubscribers();
        }

        public override void Draw()
        {
        }
    }
}