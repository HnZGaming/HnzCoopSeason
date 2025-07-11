﻿using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Merchants;
using HnzCoopSeason.Orks;
using HnzCoopSeason.Spawners;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.POI
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

        public IReadOnlyList<Poi> AllPois => _allPois;

        public void Unload()
        {
            foreach (var p in _allPois) p.Unload(true);
            _allPois.Clear();
            _spacePois.Clear();
            _planetaryPois.Clear();
        }

        public void LoadConfig(IMyCubeGrid[] sceneGrids)
        {
            var spaceOrks = SessionConfig.Instance.Orks.Where(c => c.SpawnType == SpawnType.SpaceShip).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] space orks: {spaceOrks.Select(c => c.SpawnGroups[0].SpawnGroup).ToStringSeq()}");

            var spaceMerchants = SessionConfig.Instance.PoiMerchants.Where(c => c.SpawnType == SpawnType.SpaceStation).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] space merchants: {spaceMerchants.Select(c => c.Prefab).ToStringSeq()}");

            var atmosphericOrks = SessionConfig.Instance.Orks.Where(c => c.SpawnType == SpawnType.AtmosphericShip).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] atmospheric orks: {atmosphericOrks.Select(c => c.SpawnGroups[0].SpawnGroup).ToStringSeq()}");

            var planetMerchants = SessionConfig.Instance.PoiMerchants.Where(c => c.SpawnType == SpawnType.PlanetaryStation).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] planet merchants: {planetMerchants.Select(c => c.Prefab).ToStringSeq()}");

            if (spaceOrks.Length == 0 || atmosphericOrks.Length == 0 || spaceMerchants.Length == 0 || planetMerchants.Length == 0)
            {
                MyLog.Default.Error("[HnzCoopSeason] poi map failed to reload; encounters not set");
                return;
            }

            var merchantFaction = MyAPIGateway.Session.Factions.TryGetFactionByTag("MERC");
            if (merchantFaction == null)
            {
                MyLog.Default.Error("[HnzCoopSeason] merchant faction not found");
                return;
            }

            _allPois.Clear();

            // space POIs
            foreach (var p in _spacePois.Values) p.Unload();
            _spacePois.Clear();

            var poiCountPerAxis = SessionConfig.Instance.PoiCountPerAxis;
            var poiMapRadius = SessionConfig.Instance.PoiMapRadius;
            var poiMapCenter = new Vector3D(
                SessionConfig.Instance.PoiMapCenterX,
                SessionConfig.Instance.PoiMapCenterY,
                SessionConfig.Instance.PoiMapCenterZ);

            for (var x = 0; x < poiCountPerAxis; x++)
            for (var y = 0; y < poiCountPerAxis; y++)
            for (var z = 0; z < poiCountPerAxis; z++)
            {
                var position = poiMapCenter + new Vector3D(
                    ((float)x / poiCountPerAxis * 2 - 1) * poiMapRadius,
                    ((float)y / poiCountPerAxis * 2 - 1) * poiMapRadius,
                    ((float)z / poiCountPerAxis * 2 - 1) * poiMapRadius);

                // circular shape
                if (Vector3D.Distance(position, poiMapCenter) > poiMapRadius) continue;

                // under gravity
                if (VRageUtils.CalculateNaturalGravity(position).Length() > 0)
                {
                    MyLog.Default.Info($"[HnzCoopSeason] poi removed under gravity: {x}-{y}-{z}");
                    continue;
                }

                var id = $"{x}-{y}-{z}";
                var atmospheric = PlanetCollection.HasAtmosphere(position);
                var poiConfig = new PoiConfig(id, position, atmospheric);
                var orks = atmospheric ? atmosphericOrks : spaceOrks;
                var ork = new PoiOrk(id, poiConfig.Position, orks);
                var merchant = new PoiMerchant(id, poiConfig.Position, merchantFaction, spaceMerchants);
                var poi = new Poi(poiConfig, false, new IPoiObserver[] { ork, merchant });
                _spacePois[id] = poi;
                _allPois.Add(poi);
            }

            // planetary POIs
            foreach (var p in _planetaryPois.Values) p.Unload();
            _planetaryPois.Clear();

            foreach (var p in SessionConfig.Instance.PlanetaryPois)
            {
                var orks = p.Atmospheric ? atmosphericOrks : spaceOrks;
                var ork = new PoiOrk(p.Id, p.Position, orks);
                var merchant = new PoiMerchant(p.Id, p.Position, merchantFaction, planetMerchants);
                var poi = new Poi(p, true, new IPoiObserver[] { ork, merchant });
                _planetaryPois[p.Id] = poi;
                _allPois.Add(poi);
            }

            MyLog.Default.Info($"[HnzCoopSeason] PoiMap loading POIs: {_allPois.Select(p => p.Id).ToStringSeq()}");

            foreach (var p in _allPois) p.Load(sceneGrids);
        }

        public void Update()
        {
            foreach (var p in _allPois) p.Update();
        }

        public bool TryGetPoi(string id, out Poi poi)
        {
            return _planetaryPois.TryGetValue(id, out poi) ||
                   _spacePois.TryGetValue(id, out poi);
        }

        public int GetPoiCountByState(PoiState state)
        {
            if (_allPois.Count == 0) return 0;

            var count = 0;
            foreach (var p in _allPois)
            {
                if (p.State == state)
                {
                    count += 1;
                }
            }

            return count;
        }

        public override string ToString()
        {
            return $"{nameof(AllPois)}: {AllPois.ToStringSeq()}";
        }
    }
}