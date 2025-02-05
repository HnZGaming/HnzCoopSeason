using System;
using HnzPveSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class Poi
    {
        readonly PoiConfig _config;
        readonly IPoiObserver[] _observers;
        readonly string _variableKey;

        public Poi(PoiConfig config, IPoiObserver[] observers)
        {
            _config = config;
            _observers = observers;
            _variableKey = $"HnzPveSeason.Poi.{Id}";
            State = PoiState.Occupied;
        }

        public string Id => _config.Id;
        public Vector3D Position => _config.Position;
        public PoiState State { get; private set; }

        public void Load(IMyCubeGrid[] grids) // called once
        {
            foreach (var o in _observers) o.Load(grids);

            SerializableDictionary<string, object> data;
            if (!MyAPIGateway.Utilities.GetVariable(_variableKey, out data))
            {
                data = new SerializableDictionary<string, object>();
            }

            var builder = data.Dictionary;
            var state = (PoiState)builder.GetValueOrDefault(nameof(State), (int)PoiState.Occupied);

            SetState(state, true);
        }

        public void Unload(bool sessionUnload = false) // called once
        {
            foreach (var o in _observers) o.Unload(sessionUnload);
        }

        public void SetState(PoiState state, bool init = false)
        {
            if (State == state)
            {
                if (!init) return;
            }

            if (init)
            {
                MyLog.Default.Debug($"[HnzPveSeason] POI {Id} state init with {state}");
            }
            else
            {
                MyLog.Default.Info($"[HnzPveSeason] POI {Id} state changing from {State} to {state}");
            }

            State = state;
            foreach (var o in _observers) o.OnStateChanged(State);

            if (!init)
            {
                Save();
            }
        }

        void Save()
        {
            var data = new SerializableDictionary<string, object>
            {
                [nameof(State)] = (int)State,
            };

            MyAPIGateway.Utilities.SetVariable(_variableKey, data);
            MyLog.Default.Info($"[HnzPveSeason] POI {Id} saved: {data.Dictionary.ToStringDic()}");
        }

        public void Update()
        {
            foreach (var o in _observers) o.Update();
        }
    }
}