using System;
using HnzPveSeason.Utils;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class PoiOrk : IPoiObserver
    {
        readonly string _poiId;
        readonly MesStaticEncounter _encounter;
        readonly Interval _randomInvasionInterval;
        PoiState _poiState;

        public PoiOrk(string poiId, Vector3D position, MesStaticEncounterConfig[] configs)
        {
            _poiId = poiId;
            _encounter = new MesStaticEncounter($"{poiId}-ork", "[ORKS]", configs, position, null, false);
            _randomInvasionInterval = new Interval();
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnGridSet += OnGridSet;
            _encounter.OnGridUnset += OnGridUnset;
            _encounter.Load(grids, false, true);

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
            _poiState = state;
            _encounter.SetActive(state == PoiState.Occupied);
        }

        void OnGridSet(IMyCubeGrid grid, bool recovery)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} ork spawn");
            grid.OnBlockOwnershipChanged += OnGridOwnershipChanged;
            
            if (!recovery) // new spawn
            {
                Session.Instance.OnOrkDiscovered(_poiId, grid.GetPosition());
            }
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
                Session.Instance.SetPoiState(_poiId, PoiState.Released);
            }
        }

        void AttemptRandomInvasion()
        {
            if (_poiState == PoiState.Occupied) return;

            var span = SessionConfig.Instance.RandomInvasionInterval * 60;
            if (!_randomInvasionInterval.Update(span)) return;

            if (Session.Instance.IsPlayerAroundPoi(_poiId, _encounter.Config.Area)) return;

            var chance = SessionConfig.Instance.RandomInvasionChance;
            var random = MyRandom.Instance.NextDouble();
            if (random <= chance) return;

            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} random invasion");
            Session.Instance.SetPoiState(_poiId, PoiState.Occupied);
        }

        public override string ToString()
        {
            return $"Ork({nameof(_poiId)}: {_poiId}, {nameof(_poiState)}: {_poiState}, {nameof(_encounter)}: {_encounter})";
        }
    }
}