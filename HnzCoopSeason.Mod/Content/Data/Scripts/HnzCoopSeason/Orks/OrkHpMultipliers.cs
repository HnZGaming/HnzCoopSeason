using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Orks
{
    // hp multiplier per ork grid, frozen at spawn time from the encounter's progress level.
    // values only; no entity references held, so a missed removal can't pin a grid.
    public static class OrkHpMultipliers
    {
        static readonly Dictionary<long, float> Multipliers = new Dictionary<long, float>();

        public static void Register(IMyCubeGrid grid, float multiplier)
        {
            if (grid == null || grid.Closed || grid.MarkedForClose) return;

            Multipliers[grid.EntityId] = multiplier;

            grid.OnClosing -= OnGridClosing; // no double-subscribe on re-register
            grid.OnClosing += OnGridClosing;

            MyLog.Default.Info($"[HnzCoopSeason] ork hp multiplier registered: '{grid.CustomName}' x{multiplier:0.##}");
        }

        public static bool TryGet(IMyCubeGrid grid, out float multiplier)
        {
            return Multipliers.TryGetValue(grid.EntityId, out multiplier);
        }

        public static void Clear() // session unload
        {
            Multipliers.Clear();
        }

        static void OnGridClosing(IMyEntity entity)
        {
            entity.OnClosing -= OnGridClosing;
            Multipliers.Remove(entity.EntityId);
        }
    }
}
