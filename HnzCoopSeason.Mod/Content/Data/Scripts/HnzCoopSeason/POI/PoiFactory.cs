using System.Linq;
using HnzCoopSeason.Merchants;
using HnzCoopSeason.Orks;
using HnzCoopSeason.POI.Reclaim;
using HnzCoopSeason.Spawners;
using HnzUtils;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.POI
{
    public sealed class PoiFactory
    {
        OrkConfig[] _spaceOrks;
        MerchantConfig[] _spaceMerchants;
        OrkConfig[] _atmosphericOrks;
        MerchantConfig[] _planetMerchants;

        public bool TryLoad()
        {
            _spaceOrks = SessionConfig.Instance.Orks.Where(c => c.SpawnType == SpawnType.SpaceShip).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] space orks: {_spaceOrks.Select(c => c.SpawnGroups[0].SpawnGroup).ToStringSeq()}");

            _spaceMerchants = SessionConfig.Instance.Merchants.Where(c => c.SpawnType == SpawnType.SpaceStation).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] space merchants: {_spaceMerchants.Select(c => c.Prefab).ToStringSeq()}");

            _atmosphericOrks = SessionConfig.Instance.Orks.Where(c => c.SpawnType == SpawnType.AtmosphericShip).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] atmospheric orks: {_atmosphericOrks.Select(c => c.SpawnGroups[0].SpawnGroup).ToStringSeq()}");

            _planetMerchants = SessionConfig.Instance.Merchants.Where(c => c.SpawnType == SpawnType.PlanetaryStation).ToArray();
            MyLog.Default.Info($"[HnzCoopSeason] planet merchants: {_planetMerchants.Select(c => c.Prefab).ToStringSeq()}");

            if (_spaceOrks.Length == 0 || _atmosphericOrks.Length == 0 || _spaceMerchants.Length == 0 || _planetMerchants.Length == 0)
            {
                MyLog.Default.Error("[HnzCoopSeason] poi map failed to reload; encounters not set");
                return false;
            }

            return true;
        }

        public bool TryCreateSpacePoi(string id, Vector3D position, out PoiReclaim poi)
        {
            // under gravity
            if (VRageUtils.CalculateNaturalGravity(position) != Vector3.Zero)
            {
                MyLog.Default.Info($"[HnzCoopSeason] poi removed under gravity: {id}");
                poi = null;
                return false;
            }

            //todo random poi type

            var atmospheric = PlanetCollection.HasAtmosphere(position);
            var poiConfig = new PoiConfig(id, position, false, atmospheric);
            var orks = atmospheric ? _atmosphericOrks : _spaceOrks;
            var ork = new PoiOrk(id, poiConfig.Position, orks);
            var merchant = new PoiMerchant(id, poiConfig.Position, _spaceMerchants);
            poi = new PoiReclaim(poiConfig, merchant, ork);
            return true;
        }

        public bool TryCreatePlanetaryPoi(PoiConfig config, out PoiReclaim poi)
        {
            //todo random poi type

            config.Planetary = true;
            var orks = config.Atmospheric ? _atmosphericOrks : _spaceOrks;
            var ork = new PoiOrk(config.Id, config.Position, orks);
            var merchant = new PoiMerchant(config.Id, config.Position, _planetMerchants);
            poi = new PoiReclaim(config, merchant, ork);
            return true;
        }
    }
}