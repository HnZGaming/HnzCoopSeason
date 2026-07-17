using System;
using System.Collections.Generic;
using System.Globalization;
using HnzUtils;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Orks
{
    // hp multiplier per ork grid, frozen at spawn time from the encounter's progress level.
    // values only; no entity references held, so a missed removal can't pin a grid.
    // persisted into the grid's mod storage so the value survives a world restart.
    public static class OrkHpMultipliers
    {
        static readonly Guid ModStorageKey = new Guid("f2f7a2a0-4f60-4f4e-9f0e-6f4b1a2e9c11");

        static readonly Dictionary<long, float> Multipliers = new Dictionary<long, float>();

        public static void Register(IMyCubeGrid grid, float multiplier)
        {
            if (grid == null || grid.Closed || grid.MarkedForClose) return;

            Multipliers[grid.EntityId] = multiplier;
            grid.UpdateStorageValue(ModStorageKey, multiplier.ToString(CultureInfo.InvariantCulture));

            grid.OnClosing -= OnGridClosing; // no double-subscribe on re-register
            grid.OnClosing += OnGridClosing;

            MyLog.Default.Info($"[HnzCoopSeason] ork hp multiplier registered: '{grid.CustomName}' x{multiplier:0.##}");
        }

        public static bool TryGet(IMyCubeGrid grid, out float multiplier)
        {
            if (Multipliers.TryGetValue(grid.EntityId, out multiplier)) return true;

            // grid from before a restart: recover from its mod storage, then cache
            string str;
            if (!grid.TryGetStorageValue(ModStorageKey, out str)) return false;
            if (!float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out multiplier)) return false;

            Multipliers[grid.EntityId] = multiplier;
            grid.OnClosing -= OnGridClosing;
            grid.OnClosing += OnGridClosing;
            return true;
        }

        // a stored multiplier doubles as the "this grid was ork-spawned" marker
        public static bool HasStored(IMyEntity entity)
        {
            string str;
            return entity.TryGetStorageValue(ModStorageKey, out str);
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
