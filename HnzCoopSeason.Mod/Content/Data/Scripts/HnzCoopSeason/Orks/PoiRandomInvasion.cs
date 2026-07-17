using System;
using System.Linq;
using HnzCoopSeason.POI;
using Sandbox.ModAPI;
using VRage.Library.Utils;
using VRage.Utils;

namespace HnzCoopSeason.Orks
{
    public sealed class PoiRandomInvasion
    {
        public static readonly PoiRandomInvasion Instance = new PoiRandomInvasion();

        // GameDateTime is backed by the checkpoint's ElapsedGameTime.
        const string VariableKey = "HnzCoopSeason.PoiRandomInvasion.LastWindowGameTicks";
        long _lastWindowTicks;

        public void Load()
        {
            long ticks;
            if (MyAPIGateway.Utilities.GetVariable(VariableKey, out ticks))
            {
                _lastWindowTicks = ticks;
                MyLog.Default.Info($"[HnzCoopSeason] invasion timer restored; last window ticks: {_lastWindowTicks}");
            }
            else
            {
                // fresh world: wait one full interval before the first invasion
                _lastWindowTicks = MyAPIGateway.Session.GameDateTime.Ticks;
                Save();
                MyLog.Default.Info("[HnzCoopSeason] invasion timer initialized");
            }
        }

        public void Unload()
        {
        }

        public void Update()
        {
            // throttle the poi scan to once/sec; the actual cadence uses persisted game-time below
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;

            var intervalTicks = TimeSpan.FromHours(SessionConfig.Instance.InvasionIntervalHours).Ticks;
            var nowTicks = MyAPIGateway.Session.GameDateTime.Ticks;
            if (nowTicks - _lastWindowTicks < intervalTicks) return;

            _lastWindowTicks = nowTicks;
            Save();

            if (Session.Instance.GetProgress() >= 1f)
            {
                MyLog.Default.Info("[HnzCoopSeason] aborting invasion; peace restored");
                return;
            }

            var invadedCount = Session.Instance.GetAllPois().Count(p => p.State == PoiState.Invaded);
            if (invadedCount >= SessionConfig.Instance.MaxConcurrentInvasions)
            {
                MyLog.Default.Info($"[HnzCoopSeason] aborting invasion; max concurrent reached: {invadedCount}");
                return;
            }

            var poi = Session.Instance.GetAllPois()
                .Where(p => p.State == PoiState.Released)
                .Where(p => !p.IsPlanetary) // can't have planetary poi invaded; "pending" kicks in
                .Where(p => nowTicks - p.ReleasedAtGameTicks >= intervalTicks) // grace: don't re-invade a recently-liberated poi
                .OrderBy(_ => MyRandom.Instance.NextDouble()) // random order
                .FirstOrDefault();

            if (poi == null) return; // shouldn't happen

            MyLog.Default.Error($"[HnzCoopSeason] invasion: {poi.Id}");
            if (!Session.Instance.SetPoiState(poi.Id, PoiState.Invaded))
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed to initiate invasion: {poi.Id}");
            }
        }

        void Save()
        {
            MyAPIGateway.Utilities.SetVariable(VariableKey, _lastWindowTicks);
        }
    }
}
