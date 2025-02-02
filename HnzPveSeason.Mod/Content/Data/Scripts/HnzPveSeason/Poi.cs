using System;
using HnzPveSeason.Utils;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class Poi
    {
        readonly PoiConfig _poiConfig;
        readonly MesStaticEncounter _ork;
        readonly MesStaticEncounter _merchant;

        public Poi(PoiConfig poiConfig, MesStaticEncounterConfig[] orkConfigs, MesStaticEncounterConfig[] merchantConfigs)
        {
            _poiConfig = poiConfig;
            CurrentState = PoiState.Occupied;
            _ork = new MesStaticEncounter($"{Id}-ork", orkConfigs, poiConfig.Position);
            _merchant = new MesStaticEncounter($"{Id}-merchant", merchantConfigs, poiConfig.Position);
        }

        public string Id => _poiConfig.Id;
        public Vector3D Position => _poiConfig.Position;
        public PoiState CurrentState { get; private set; }

        public void Load(IMyCubeGrid[] grids) // called once
        {
            _ork.OnSpawned += OnOrkSpawned;
            _ork.OnDespawned += OnOrkDespawned;

            _merchant.OnSpawned += OnMerchantSpawned;
            _merchant.OnDespawned += OnMerchantDespawned;

            _ork.Load(grids);
            _merchant.Load(grids);

            PoiBuilder builder;
            if (PoiBuilder.TryLoad(Id, out builder))
            {
                MyLog.Default.Info($"[HnzPveSeason] POI {Id} recovered");
            }
            else
            {
                builder = new PoiBuilder { CurrentState = PoiState.Occupied };
            }

            SetState(builder.CurrentState, true);
        }

        public void Unload() // called once
        {
            _ork.Unload();
            _merchant.Unload();

            _ork.OnSpawned -= OnOrkSpawned;
            _ork.OnDespawned -= OnOrkDespawned;

            _merchant.OnSpawned -= OnMerchantSpawned;
            _merchant.OnDespawned -= OnMerchantDespawned;
        }

        void SetState(PoiState state, bool init = false)
        {
            if (CurrentState == state)
            {
                if (!init) return;
            }

            if (!init)
            {
                MyLog.Default.Info($"[HnzPveSeason] POI {Id} state changing from {CurrentState} to {state}");
            }

            CurrentState = state;
            _ork.SetActive(CurrentState == PoiState.Occupied);
            _merchant.SetActive(CurrentState == PoiState.Released);

            if (CurrentState == PoiState.Released)
            {
                _ork.Despawn();
            }

            if (!init)
            {
                Save();
            }
        }

        void Save()
        {
            var builder = new PoiBuilder { CurrentState = CurrentState };
            PoiBuilder.Save(Id, builder);
            MyLog.Default.Info($"[HnzPveSeason] POI {Id}` saved");
        }

        public void Update()
        {
            _ork.Update();
            _merchant.Update();
        }

        public void Release()
        {
            SetState(PoiState.Released);
        }

        void OnOrkSpawned(IMyCubeGrid grid)
        {
            grid.OnBlockOwnershipChanged += OnOrkOwnershipChanged;
        }

        void OnOrkDespawned(IMyCubeGrid grid)
        {
            grid.OnBlockOwnershipChanged -= OnOrkOwnershipChanged;
        }

        void OnOrkOwnershipChanged(IMyCubeGrid grid)
        {
            if (!VRageUtils.IsGridControlledByAI(grid))
            {
                Session.Instance.ReleasePoi(Id);
            }
        }

        void OnMerchantSpawned(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {Id} merchant spawn");
        }

        void OnMerchantDespawned(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {Id} merchant despawn");
        }
    }
}