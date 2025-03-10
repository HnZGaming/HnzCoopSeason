using System;
using System.Collections.Generic;
using HnzCoopSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class Poi : IPoi
    {
        readonly PoiConfig _config;
        readonly IPoiObserver[] _observers;
        readonly string _variableKey;

        public Poi(PoiConfig config, bool isPlanetary, IPoiObserver[] observers)
        {
            IsPlanetary = isPlanetary;
            _config = config;
            _observers = observers;
            _variableKey = $"HnzCoopSeason.Poi.{Id}";
            State = PoiState.Occupied;
        }

        public string Id => _config.Id;
        public Vector3D Position => _config.Position;
        public bool IsPlanetary { get; }
        public PoiState State { get; private set; }
        public IReadOnlyList<IPoiObserver> Observers => _observers;

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

        public bool SetState(PoiState state, bool init = false)
        {
            if (State == state)
            {
                if (!init) return false;
            }

            if (init)
            {
                MyLog.Default.Debug("[HnzCoopSeason] POI {0} state init with {1}", Id, state);
            }
            else
            {
                MyLog.Default.Info($"[HnzCoopSeason] POI {Id} state changing from {State} to {state}");
            }

            State = state;
            foreach (var o in _observers) o.OnStateChanged(State);

            if (!init)
            {
                Save();
            }

            return true;
        }

        void Save()
        {
            var data = new SerializableDictionary<string, object>
            {
                [nameof(State)] = (int)State,
            };

            MyAPIGateway.Utilities.SetVariable(_variableKey, data);
            MyLog.Default.Info($"[HnzCoopSeason] POI {Id} saved: {data.Dictionary.ToStringDic()}");
        }

        public void Update()
        {
            foreach (var o in _observers) o.Update();
        }

        public bool IsPlayerAround(float radius)
        {
            var sphere = new BoundingSphereD(Position, radius);
            return OnlineCharacterCollection.ContainsPlayer(sphere);
        }

        public override string ToString()
        {
            return $"POI({nameof(Id)}: {Id}, {nameof(State)}: {State}, {nameof(_observers)}: {_observers.ToStringSeq()})";
        }
    }
}