using System;
using HnzCoopSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class PoiOrk : IPoiObserver
    {
        readonly string _poiId;
        readonly MesEncounter _encounter;
        readonly Interval _randomInvasionInterval;

        public PoiOrk(string poiId, Vector3D position, MesEncounterConfig[] configs)
        {
            _poiId = poiId;
            _encounter = new MesEncounter($"{poiId}-ork", configs, position);
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
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _encounter.SetActive(state == PoiState.Occupied);
        }

        void OnMainGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} spawn");
            grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;

            foreach (var beacon in grid.GetFatBlocks<IMyBeacon>())
            {
                beacon.HudText = $"[BOSS] {grid.CustomName}";
            }

            foreach (var antenna in grid.GetFatBlocks<IMyRadioAntenna>())
            {
                antenna.HudText = $"[BOSS] {grid.CustomName}";
            }

            Session.Instance.OnOrkDiscovered(_poiId, grid.GetPosition());
        }

        void OnMainGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} despawn");
            grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
        }

        void OnBlockOwnershipChanged(IMyCubeGrid grid)
        {
            PoiState state;
            if (!Session.Instance.TryGetPoiState(_poiId, out state)) return; // shouldn't happen

            if (state == PoiState.Released) return;
            if (VRageUtils.IsGridControlledByAI(grid)) return;

            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} defeated by players");
            Session.Instance.SetPoiState(_poiId, PoiState.Released);
        }

        public void Spawn(int configIndex)
        {
            _encounter.ForceSpawn(configIndex);
        }

        public override string ToString()
        {
            return $"Ork({nameof(_poiId)}: {_poiId},  {nameof(_encounter)}: {_encounter})";
        }
    }
}