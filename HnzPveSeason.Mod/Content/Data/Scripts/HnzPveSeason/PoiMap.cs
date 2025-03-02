﻿using System;
using System.Collections.Generic;
using System.Linq;
using HnzPveSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class PoiMap
    {
        readonly Dictionary<string, Poi> _spacePois;
        readonly Dictionary<string, Poi> _planetaryPois;
        readonly List<Poi> _allPois;

        public PoiMap()
        {
            _spacePois = new Dictionary<string, Poi>();
            _planetaryPois = new Dictionary<string, Poi>();
            _allPois = new List<Poi>();
        }

        public IEnumerable<Poi> AllPois => _allPois;

        public void Unload()
        {
            foreach (var p in _allPois) p.Unload();
            _allPois.Clear();
            _spacePois.Clear();
            _planetaryPois.Clear();
        }

        public void LoadConfig()
        {
            var spaceOrks = SessionConfig.Instance.Orks.Where(c => !c.Planetary).ToArray();
            MyLog.Default.Info($"[HnzPveSeason] space orks: {spaceOrks.Select(c => c.SpawnGroup).ToStringSeq()}");

            var spaceMerchants = SessionConfig.Instance.Merchants.Where(c => !c.Planetary).ToArray();
            MyLog.Default.Info($"[HnzPveSeason] space merchants: {spaceMerchants.Select(c => c.SpawnGroup).ToStringSeq()}");

            var planetOrks = SessionConfig.Instance.Orks.Where(c => c.Planetary).ToArray();
            MyLog.Default.Info($"[HnzPveSeason] planet orks: {planetOrks.Select(c => c.SpawnGroup).ToStringSeq()}");

            var planetMerchants = SessionConfig.Instance.Merchants.Where(c => c.Planetary).ToArray();
            MyLog.Default.Info($"[HnzPveSeason] planet merchants: {planetMerchants.Select(c => c.SpawnGroup).ToStringSeq()}");

            if (spaceOrks.Length == 0 || planetOrks.Length == 0 || spaceMerchants.Length == 0 || planetMerchants.Length == 0)
            {
                MyLog.Default.Error("[HnzPveSeason] poi map failed to reload; encounters not set");
                return;
            }

            var random = new Random(0);

            _allPois.Clear();

            // space POIs
            foreach (var p in _spacePois.Values) p.Unload();
            _spacePois.Clear();

            var poiCountPerAxis = SessionConfig.Instance.PoiCountPerAxis;
            var poiMapRadius = SessionConfig.Instance.PoiMapRadius;
            for (var x = 0; x < poiCountPerAxis; x++)
            for (var y = 0; y < poiCountPerAxis; y++)
            for (var z = 0; z < poiCountPerAxis; z++)
            {
                var position = new Vector3D(
                    ((float)x / poiCountPerAxis * 2 - 1) * poiMapRadius,
                    ((float)y / poiCountPerAxis * 2 - 1) * poiMapRadius,
                    ((float)z / poiCountPerAxis * 2 - 1) * poiMapRadius);

                // circular shape
                if (Vector3D.Distance(position, Vector3D.Zero) > poiMapRadius) continue;

                // under gravity
                if (VRageUtils.CalculateNaturalGravity(position).Length() > 0)
                {
                    MyLog.Default.Info($"[HnzPveSeason] poi removed under gravity: {x}, {y}, {z}");
                    continue;
                }

                var id = $"{x}-{y}-{z}";
                var poiConfig = new PoiConfig(id, position);
                var ork = new PoiOrk(id, poiConfig.Position, spaceOrks);
                var faction = Economy.Instance.GetFaction(random.Next());
                var merchant = new PoiMerchant(id, poiConfig.Position, spaceMerchants, faction);
                var poi = new Poi(poiConfig, new IPoiObserver[] { ork, merchant });
                _spacePois[id] = poi;
                _allPois.Add(poi);
            }

            // planetary POIs
            foreach (var p in _planetaryPois.Values) p.Unload();
            _planetaryPois.Clear();

            foreach (var p in SessionConfig.Instance.PlanetaryPois)
            {
                var ork = new PoiOrk(p.Id, p.Position, planetOrks);
                var faction = Economy.Instance.GetFaction(random.Next());
                var merchant = new PoiMerchant(p.Id, p.Position, planetMerchants, faction);
                var poi = new Poi(p, new IPoiObserver[] { ork, merchant });
                _planetaryPois[p.Id] = poi;
                _allPois.Add(poi);
            }

            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            var grids = entities.OfType<IMyCubeGrid>().ToArray();
            foreach (var p in _allPois) p.Load(grids);
        }

        public void Update()
        {
            foreach (var p in _allPois) p.Update();
        }

        public bool TryGetPoi(string id, out Poi poi)
        {
            if (_planetaryPois.TryGetValue(id, out poi)) return true;
            if (_spacePois.TryGetValue(id, out poi)) return true;
            return false;
        }
    }
}