using System.Linq;
using Sandbox.ModAPI;
using VRage.Library.Utils;
using VRage.Utils;

namespace HnzCoopSeason.POI
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

            if (Session.Instance.GetAllPois().Any(p => p.State == PoiState.Invaded))
            {
                MyLog.Default.Info("[HnzCoopSeason] aborting invasion; already got one");
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