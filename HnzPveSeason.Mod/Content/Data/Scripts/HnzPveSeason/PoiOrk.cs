using HnzPveSeason.Utils;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class PoiOrk : IPoiObserver
    {
        readonly string _id;
        readonly MesStaticEncounter _encounter;

        public PoiOrk(string id, Vector3D position, MesStaticEncounterConfig[] configs)
        {
            _id = id;
            _encounter = new MesStaticEncounter($"{id}-ork", configs, position);
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
            MyLog.Default.Info($"[HnzPveSeason] POI {_id} ork spawn");
            grid.OnBlockOwnershipChanged += OnGridOwnershipChanged;
        }

        void OnGridDespawned(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_id} ork despawn");
            grid.OnBlockOwnershipChanged -= OnGridOwnershipChanged;
        }

        void OnGridOwnershipChanged(IMyCubeGrid grid)
        {
            if (!VRageUtils.IsGridControlledByAI(grid))
            {
                Session.Instance.ReleasePoi(_id);
            }
        }
    }
}