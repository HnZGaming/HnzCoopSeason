using System;
using System.Linq;
using HnzPveSeason.Utils;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class PoiOrk : IPoiObserver
    {
        readonly string _poiId;
        readonly MesStaticEncounter _encounter;
        readonly Interval _randomInvasionInterval;
        readonly Random _randomInvasionChance;

        public PoiOrk(string poiId, Vector3D position, MesStaticEncounterConfig[] configs)
        {
            _poiId = poiId;
            _encounter = new MesStaticEncounter($"{poiId}-ork", "[ORKS]", configs, position, false);
            _randomInvasionInterval = new Interval();
            _randomInvasionChance = new Random();
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnGridSet += OnGridSet;
            _encounter.OnGridUnset += OnGridUnset;
            _encounter.Load(grids);

            _randomInvasionInterval.Initialize();
        }

        void IPoiObserver.Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
            _encounter.OnGridSet -= OnGridSet;
            _encounter.OnGridUnset -= OnGridUnset;
        }

        void IPoiObserver.Update()
        {
            _encounter.Update();
            AttemptRandomInvasion();
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _encounter.SetActive(state == PoiState.Occupied);
        }

        void OnGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} ork spawn");
            grid.OnBlockOwnershipChanged += OnGridOwnershipChanged;
        }

        void OnGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} ork despawn");
            grid.OnBlockOwnershipChanged -= OnGridOwnershipChanged;
        }

        void OnGridOwnershipChanged(IMyCubeGrid grid)
        {
            if (!VRageUtils.IsGridControlledByAI(grid))
            {
                Session.Instance.GetPoi(_poiId).SetState(PoiState.Released);
            }
        }

        void AttemptRandomInvasion()
        {
            var poi = Session.Instance.GetPoi(_poiId);
            if (poi.State == PoiState.Occupied) return;

            var interval = TimeSpan.FromSeconds(SessionConfig.Instance.RandomInvasionInterval);
            if (!_randomInvasionInterval.Update(interval)) return;

            var chance = SessionConfig.Instance.RandomInvasionChance;
            var random = _randomInvasionChance.NextDouble();
            if (random <= chance) return;

            var merchant = poi.Observers.OfType<PoiMerchant>().First();
            if (merchant.LastPlayerVisitTime + interval > DateTime.UtcNow) return;

            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} random invasion");
            poi.SetState(PoiState.Occupied);
        }
    }
}