using System;
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
        readonly Dictionary<Vector3I, Poi> _spacePois;
        readonly Dictionary<string, Poi> _planetaryPois;

        public PoiMap()
        {
            _spacePois = new Dictionary<Vector3I, Poi>();
            _planetaryPois = new Dictionary<string, Poi>();
        }

        public Poi[] GetAllPois()
        {
            return _spacePois.Values.Concat(_planetaryPois.Values).ToArray();
        }

        public void Unload()
        {
            foreach (var p in _spacePois.Values) p.Unload();
            _spacePois.Clear();

            foreach (var p in _planetaryPois.Values) p.Unload();
            _planetaryPois.Clear();
        }

        public void LoadConfig()
        {
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

                var poiConfig = new PoiConfig
                {
                    Id = $"{x}-{y}-{z}",
                    Position = position,
                };

                var poi = new Poi(poiConfig, SpawnEnvironment.Space, SessionConfig.Instance.Orks);
                _spacePois[new Vector3I(x, y, z)] = poi;
            }

            // planetary POIs
            foreach (var p in _planetaryPois.Values) p.Unload();
            _planetaryPois.Clear();

            foreach (var p in SessionConfig.Instance.PlanetaryPois)
            {
                var poi = new Poi(p, SpawnEnvironment.Planet, SessionConfig.Instance.Orks);
                _planetaryPois[p.Id] = poi;
            }
        }

        public void LoadScene()
        {
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            var grids = entities.OfType<IMyCubeGrid>().ToArray();
            foreach (var p in GetAllPois()) p.Load(grids);
        }

        public void Update()
        {
            foreach (var p in _spacePois.Values) p.Update();
        }

        public void ReleasePoi(string id)
        {
            Poi poi;
            if (!TryGetPoi(id, out poi))
            {
                MyLog.Default.Error($"[HnzPveSeason] POI not found by id: {id}");
                return;
            }

            poi.Release();
        }

        bool TryGetPoi(string id, out Poi poi)
        {
            if (_planetaryPois.TryGetValue(id, out poi)) return true;

            foreach (var p in _spacePois.Values)
            {
                if (p.Id == id)
                {
                    poi = p;
                    return true;
                }
            }

            return false;
        }
    }
}