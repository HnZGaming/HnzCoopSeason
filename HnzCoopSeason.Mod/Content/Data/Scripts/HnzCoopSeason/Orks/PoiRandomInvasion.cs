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

        public void Load()
        {
        }

        public void Unload()
        {
        }

        public void Update()
        {
            var interval = SessionConfig.Instance.InvasionIntervalHours * 60 * 60 * 60;
            if (MyAPIGateway.Session.GameplayFrameCounter % interval != 0) return;

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
                .OrderBy(_ => MyRandom.Instance.NextDouble()) // random order
                .FirstOrDefault();

            if (poi == null) return; // shouldn't happen

            MyLog.Default.Error($"[HnzCoopSeason] invasion: {poi.Id}");
            if (!Session.Instance.SetPoiState(poi.Id, PoiState.Invaded))
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed to initiate invasion: {poi.Id}");
            }
        }
    }
}