using System;
using HnzCoopSeason.Utils;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class PoiOrk : IPoiObserver
    {
        readonly string _poiId;
        readonly MesEncounter _encounter;
        readonly Interval _randomInvasionInterval;
        PoiState _poiState;

        public PoiOrk(string poiId, Vector3D position, MesEncounterConfig[] configs)
        {
            _poiId = poiId;
            _encounter = new MesEncounter($"{poiId}-ork", "[ORKS]", configs, position, null);
            _randomInvasionInterval = new Interval();
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnMainGridSet += OnMainGridSet;
            _encounter.OnMainGridUnset += OnMainGridUnset;
            _encounter.Load(grids);

            _randomInvasionInterval.Initialize();
        }

        void IPoiObserver.Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
            _encounter.OnMainGridSet -= OnMainGridSet;
            _encounter.OnMainGridUnset -= OnMainGridUnset;
        }

        void IPoiObserver.Update()
        {
            _encounter.Update();
            AttemptRandomInvasion();
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _poiState = state;
            _encounter.SetActive(state == PoiState.Occupied);
        }

        void OnMainGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} spawn");
            grid.OnBlockOwnershipChanged += OnGridOwnershipChanged;

            Session.Instance.OnOrkDiscovered(_poiId, grid.GetPosition());
        }

        void OnMainGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} despawn");
            grid.OnBlockOwnershipChanged -= OnGridOwnershipChanged;
        }

        void OnGridOwnershipChanged(IMyCubeGrid grid)
        {
            if (!VRageUtils.IsGridControlledByAI(grid))
            {
                MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} defeated by players");
                Session.Instance.SetPoiState(_poiId, PoiState.Released);
            }
        }

        void AttemptRandomInvasion()
        {
            if (_poiState == PoiState.Occupied) return;

            var span = SessionConfig.Instance.RandomInvasionInterval * 60;
            if (!_randomInvasionInterval.Update(span)) return;

            if (Session.Instance.IsPlayerAroundPoi(_poiId, SessionConfig.Instance.EncounterRadius)) return;

            var chance = SessionConfig.Instance.RandomInvasionChance;
            var random = MyRandom.Instance.NextDouble();
            if (random <= chance) return;

            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} random invasion");
            Session.Instance.SetPoiState(_poiId, PoiState.Occupied);
        }

        public void Spawn(int configIndex)
        {
            _encounter.Spawn(configIndex);
        }

        public override string ToString()
        {
            return $"Ork({nameof(_poiId)}: {_poiId}, {nameof(_poiState)}: {_poiState}, {nameof(_encounter)}: {_encounter})";
        }
    }
}