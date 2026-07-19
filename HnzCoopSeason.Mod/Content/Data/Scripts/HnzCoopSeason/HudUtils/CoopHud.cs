using System;
using HnzCoopSeason.NPC;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.HudUtils
{
    /// <summary>
    ///     Client-side HUD root: owns the RichHud MeterPanel and the mod's
    ///     F2 (Rich HUD Terminal) settings page. Load/Unload from the
    ///     RichHudClient init/closed callbacks only.
    /// </summary>
    public static class CoopHud
    {
        const string ConfigFileName = "HnzCoopSeason.HudConfig.xml";

        static MeterPanel _meterPanel;
        static TargetReticle[] _targetReticles;
        static CoopSettingsWindow _settingsWindow;
        static HudConfig _config = new HudConfig();

        public static bool ShowTargetReticle => _config.ShowTargetReticle;
        public static float ReticleFov => _config.ReticleFov;
        public static int MinTargetBlocks => _config.MinTargetBlocks;
        public static double ReticleMinDistance => _config.ReticleMinDistance;

        /// <summary>
        ///     True while the meter should stand down for WeaponCore's target panel.
        ///     Opt-in: without it the meter stays up alongside WC (config default).
        /// </summary>
        public static bool YieldToWeaponCore => _config.HideWithWeaponCore && WcHudApi.HasFocusTarget();

        internal static HudConfig Config => _config;
        internal static MeterPanel Meter => _meterPanel; // settings window parks itself under the meter

        internal static void ApplyConfigNow() => ApplyConfig();
        internal static void SaveNow() => SaveConfig(_config);

        public static void Load()
        {
            MyLog.Default.Info("[HnzCoopSeason] CoopHud.Load()");

            _config = LoadConfig();
            _meterPanel = new MeterPanel(HudMain.HighDpiRoot);
            _targetReticles = new TargetReticle[TargetReticle.PoolSize];
            for (var i = 0; i < _targetReticles.Length; i++)
            {
                _targetReticles[i] = new TargetReticle(HudMain.HighDpiRoot, i);
            }
            _settingsWindow = new CoopSettingsWindow(HudMain.HighDpiRoot);
            ApplyConfig();
            CreateTerminalPage();
        }

        public static void Unload()
        {
            MyLog.Default.Info("[HnzCoopSeason] CoopHud.Unload()");
            _meterPanel?.Unregister();
            _meterPanel = null;
            TargetReticle.Clear();
            if (_targetReticles != null)
            {
                foreach (var reticle in _targetReticles)
                {
                    reticle?.Unregister();
                }

                _targetReticles = null;
            }
            _settingsWindow?.Close();
            _settingsWindow = null;
        }

        static void ApplyConfig()
        {
            ScreenTopHud.Instance.SetEnabled(nameof(ProgressionView), _config.ShowProgressMeter);
            ScreenTopHud.Instance.SetEnabled(nameof(NpcHud), _config.ShowCaptureMeter);

            if (_meterPanel != null)
            {
                _meterPanel.TopMargin = _config.MeterTopMargin;
                _meterPanel.Anchor = _config.MeterAnchor;
                _meterPanel.Minimal = _config.MinimalMeter;
            }
        }

        static void CreateTerminalPage() // F2 menu: just a launcher for the custom window
        {
            var openButton = new TerminalButton
            {
                Name = "Open HUD settings",
                ToolTip = "Opens the COOP HUD settings window. Click again to close it.",
            };

            openButton.ControlChanged += (sender, e) =>
            {
                // toggle: a second click closes the window instead of re-opening it
                var opening = _settingsWindow != null && !_settingsWindow.IsOpen;
                _settingsWindow?.Toggle();

                if (opening) RichHudTerminal.CloseMenu(); // get the terminal out of the way
            };

            var category = new ControlCategory
            {
                HeaderText = "HUD",
                SubheaderText = "On-screen displays of the COOP season",
                TileContainer = { new ControlTile { openButton } },
            };

            var page = new ControlPage { Name = "Settings" };
            page.Add(category);
            RichHudTerminal.Root.Add(page);
            RichHudTerminal.Root.Enabled = true; // mod roots are hidden in the F2 terminal until enabled
        }

        static HudConfig LoadConfig()
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInLocalStorage(ConfigFileName, typeof(HudConfig)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(ConfigFileName, typeof(HudConfig)))
                    {
                        return MyAPIGateway.Utilities.SerializeFromXML<HudConfig>(reader.ReadToEnd()) ?? new HudConfig();
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed loading hud config; using defaults; {e}");
            }

            return new HudConfig();
        }

        static void SaveConfig(HudConfig config)
        {
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(ConfigFileName, typeof(HudConfig)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(config));
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed saving hud config; {e}");
            }
        }

        public sealed class HudConfig
        {
            public bool ShowProgressMeter = true;
            public bool ShowCaptureMeter = true;
            public bool ShowTargetReticle = true;
            public float MeterTopMargin = 50;
            public float ReticleFov = 10; // half-angle from screen centre, degrees, within which a grid can be targeted
            public int MinTargetBlocks = 10; // grids smaller than this are wreckage, not targets; 0 disables the filter
            public float ReticleMinDistance = 0; // brackets hidden closer than this, in metres; 0 keeps them on all the way in
            public MeterAnchor MeterAnchor = MeterAnchor.Left;
            public bool HideWithWeaponCore = false; // yield the meter slot while WC draws its target panel
            public bool MinimalMeter = false; // drop the meters' subtitle/description lines, keeping title + bar

            // settings window placement (HighDpiRoot px, 1080p-normalized). Until the player
            // drags it, the window tracks the meter instead of using a stored position.
            public bool SettingsWindowMoved = false;
            public Vector2 SettingsWindowOffset = Vector2.Zero;
        }
    }
}
