using System;
using System.Linq;
using GridStorage.API;
using HnzCoopSeason.HudUtils;
using HnzUtils;
using Sandbox.ModAPI;

namespace HnzCoopSeason.NPC
{
    public sealed class NpcHud
    {
        #region Fields

        public static readonly NpcHud Instance = new NpcHud();

        MeterState _state;

        #endregion

        #region Lifecycle

        public void Load()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            _state = new MeterState
            {
                BarName = "CAPMETER",
                ShowInfoIcon = true,
            };

            ScreenTopHud.Instance.AddGroup(nameof(NpcHud), _state, 1);
        }

        public void Unload()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            NpcTargetFinder.Clear();
            ScreenTopHud.Instance.RemoveGroup(nameof(NpcHud));
            _state = null;
        }

        public void Update()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            NpcTargetFinder.RefreshTransforms();

            if (MyAPIGateway.Session.GameplayFrameCounter % 5 != 0) return;

            var target = NpcTargetFinder.Scan(!CoopHud.YieldToWeaponCore);
            var canRender = target.Grid != null && ApplyMeter(target);
            ScreenTopHud.Instance.SetActive(nameof(NpcHud), canRender);
            NpcTargetFinder.PushReticles();
        }

        #endregion

        #region Meter

        bool ApplyMeter(NpcTargetFinder.Result target) // false if deactivating the view
        {
            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(target.Grid, out state)) return false;

            var player = MyAPIGateway.Session.Player;
            if (player == null) return false;

            var playerGroup = CoopGridTakeover.GetPlayerGroup(player.IdentityId);
            var takeoverReady = state.CanTakeOver && (state.TakeoverPlayerGroup == 0 || state.TakeoverPlayerGroup == playerGroup);

            var remaining = state.Controllers.Count(id => id != 0 && id != playerGroup);
            var takeoverTargetCount = Math.Max(state.MaxControllers, state.Controllers.Length);

            _state.Title = target.Grid.CustomName;
            _state.Subtitle = target.IsBoss ? "This is the boss Ork! Neutralize it to reclaim the trading hub!" : "";
            _state.SubtitleHighlight = target.IsBoss ? "boss Ork" : null;
            _state.Description = !takeoverReady
                ? "To neutralize a wild grid, take over its remote blocks and control seats."
                : "You can capture a neutralized grid into a garage block.";
            _state.Progress = takeoverTargetCount == 0 ? 0 : (double)remaining / takeoverTargetCount;
            _state.ValueText = $"{remaining}/{takeoverTargetCount}";
            _state.HealthStyle = true;
            _state.CompleteText = takeoverReady ? "READY TO CAPTURE" : null;

            return true;
        }

        #endregion
    }
}
