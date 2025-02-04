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

        public PoiOrk(string poiId, Vector3D position, MesStaticEncounterConfig[] configs)
        {
            _poiId = poiId;
            _encounter = new MesStaticEncounter($"{poiId}-ork", "[ORKS]", configs, position, false);
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnSpawned += OnGridSpawned;
            _encounter.OnDespawned += OnGridDespawned;
            _encounter.Load(grids);
        }

        void IPoiObserver.Unload()
        {
            _encounter.Unload();
            _encounter.OnSpawned -= OnGridSpawned;
            _encounter.OnDespawned -= OnGridDespawned;
        }

        void IPoiObserver.Update()
        {
            _encounter.Update();
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _encounter.SetActive(state == PoiState.Occupied);
            
            if (state == PoiState.Released)
            {
                _encounter.Despawn();
            }
        }

        void OnGridSpawned(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} ork spawn");
            grid.OnBlockOwnershipChanged += OnGridOwnershipChanged;
        }

        void OnGridDespawned(IMyCubeGrid grid)
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
    }
}