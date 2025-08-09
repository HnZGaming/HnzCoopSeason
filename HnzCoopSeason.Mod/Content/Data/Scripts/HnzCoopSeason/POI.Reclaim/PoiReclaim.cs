using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.POI.Reclaim
{
    public sealed class PoiReclaim : IPoi
    {
        readonly PoiConfig _config;
        readonly PoiMerchant _merchant;
        readonly PoiOrk _ork;
        readonly PoiStateSerializer _state;

        public PoiReclaim(PoiConfig config, PoiMerchant merchant, PoiOrk ork)
        {
            _config = config;
            _merchant = merchant;
            _ork = ork;
            _state = new PoiStateSerializer(config.Id);
        }

        public string Id => _config.Id;
        public Vector3D Position => _config.Position;
        public bool IsPlanetary => _config.Planetary;
        public PoiState State => _state.State;

        public void Load(IMyCubeGrid[] grids) // called once
        {
            _merchant.Load(grids);
            _ork.Load(grids);

            _state.Load();
            ForceSetState(_state.State);
        }

        public void Unload(bool sessionUnload) // called once
        {
            _merchant.Unload(sessionUnload);
            _ork.Unload(sessionUnload);
        }

        // must be called from Session to keep other things in sync
        public bool TrySetState(PoiState state)
        {
            if (State == state) return false;

            ForceSetState(state);
            return true;
        }

        void ForceSetState(PoiState state)
        {
            MyLog.Default.Info($"[HnzCoopSeason] POI {Id} state changing from {State} to {state}");
            _state.State = state;
            _merchant.OnStateChanged(State);
            _ork.OnStateChanged(State);
        }

        public bool TryGetEntityPosition(out Vector3D position)
        {
            if (_merchant.TryGetPosition(out position)) return true;
            if (_ork.TryGetPosition(out position)) return true;

            position = default(Vector3D);
            return false;
        }

        public void Save()
        {
            _state.Save();
            _merchant.Save();
            _ork.Save();
            MyLog.Default.Info($"[HnzCoopSeason] POI {Id} saved: {_state}");
        }

        public void Update()
        {
            _merchant.Update();
            _ork.Update();
        }

        public void Spawn(int configIndex)
        {
            if (State == PoiState.Released)
            {
                _merchant.Spawn(configIndex);
            }
            else
            {
                _ork.Spawn(configIndex);
            }
        }

        public override string ToString()
        {
            return $"POI({nameof(Id)}: {Id}, {nameof(State)}: {State}, {nameof(_merchant)}: {_merchant}, {nameof(_ork)}: {_ork})";
        }
    }
}