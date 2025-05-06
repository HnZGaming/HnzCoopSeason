using System;
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
        MissionListElement _missionElement;

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
            header.Height = 30f;
            body.Size = new Vector2(500f, 200f);

            _keyBinds = BindManager.GetOrCreateGroup("CoopContractBinds");
            _keyBinds.RegisterBinds(new BindGroupInitializer
            {
                { "CoopContractToggle", MyKeys.OemTilde }
            });

            _keyBinds[0].NewPressed += ToggleWindow;

            _missionElement = new MissionListElement(bodyBg);
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
            _missionElement.SetMission(missions[0]);
        }
    }
}