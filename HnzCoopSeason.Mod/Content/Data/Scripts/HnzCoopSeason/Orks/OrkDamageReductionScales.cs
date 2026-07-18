using System;
using System.Collections.Generic;
using System.Globalization;
using HnzUtils;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Orks
{
    // damage reduction scale per ork grid, frozen at spawn time from the encounter's progress level.
    // values only; no entity references held, so a missed removal can't pin a grid.
    // persisted into the grid's mod storage so the value survives a world restart.
    public static class OrkDamageReductionScales
    {
        static readonly Guid ModStorageKey = new Guid("f2f7a2a0-4f60-4f4e-9f0e-6f4b1a2e9c11");

        static readonly Dictionary<long, float> Scales = new Dictionary<long, float>();

        public static void Register(IMyCubeGrid grid, float scale)
        {
            if (grid == null || grid.Closed || grid.MarkedForClose) return;

            Scales[grid.EntityId] = scale;
            grid.UpdateStorageValue(ModStorageKey, scale.ToString(CultureInfo.InvariantCulture));

            grid.OnClosing -= OnGridClosing; // no double-subscribe on re-register
            grid.OnClosing += OnGridClosing;

            MyLog.Default.Info($"[HnzCoopSeason] ork damage reduction scale registered: '{grid.CustomName}' x{scale:0.##}");
        }

        public static bool TryGet(IMyCubeGrid grid, out float scale)
        {
            if (Scales.TryGetValue(grid.EntityId, out scale)) return true;

            // grid from before a restart: recover from its mod storage, then cache
            string str;
            if (!grid.TryGetStorageValue(ModStorageKey, out str)) return false;
            if (!float.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out scale)) return false;

            Scales[grid.EntityId] = scale;
            grid.OnClosing -= OnGridClosing;
            grid.OnClosing += OnGridClosing;
            return true;
        }

        // a stored scale doubles as the "this grid was ork-spawned" marker
        public static bool HasStored(IMyEntity entity)
        {
            string str;
            return entity.TryGetStorageValue(ModStorageKey, out str);
        }

        public static void Clear() // session unload
        {
            Scales.Clear();
        }

        static void OnGridClosing(IMyEntity entity)
        {
            entity.OnClosing -= OnGridClosing;
            Scales.Remove(entity.EntityId);
        }
    }
}
