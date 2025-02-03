using System;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class Poi
    {
        readonly PoiConfig _config;
        readonly IPoiObserver[] _observers;

        public Poi(PoiConfig config, IPoiObserver[] observers)
        {
            _config = config;
            _observers = observers;
            CurrentState = PoiState.Occupied;
        }

        public string Id => _config.Id;
        public Vector3D Position => _config.Position;
        public PoiState CurrentState { get; private set; }

        public void Load(IMyCubeGrid[] grids) // called once
        {
            foreach (var o in _observers) o.Load(grids);

            PoiBuilder builder;
            if (PoiBuilder.TryLoad(Id, out builder))
            {
                MyLog.Default.Info($"[HnzPveSeason] POI {Id} recovered from save");
            }
            else
            {
                builder = new PoiBuilder { CurrentState = PoiState.Occupied };
            }

            SetState(builder.CurrentState, true);
        }

        public void Unload() // called once
        {
            foreach (var o in _observers) o.Unload();
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
            foreach (var o in _observers) o.OnStateChanged(state);

            if (!init)
            {
                Save();
            }
        }

        void Save()
        {
            var builder = new PoiBuilder { CurrentState = CurrentState };
            PoiBuilder.Save(Id, builder);
            MyLog.Default.Info($"[HnzPveSeason] POI {Id} saved");
        }

        public void Update()
        {
            foreach (var o in _observers) o.Update();
        }

        public void Release()
        {
            SetState(PoiState.Released);
        }
    }
}