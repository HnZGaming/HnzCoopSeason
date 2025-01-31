using System;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class Poi
    {
        readonly PoiConfig _poiConfig;

        public Poi(PoiConfig poiConfig)
        {
            _poiConfig = poiConfig;
        }

        public string Id => _poiConfig.Id;
        public Vector3D Position => _poiConfig.Position;
    }
}