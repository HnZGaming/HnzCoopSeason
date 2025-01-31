using System;
using HnzPveSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class Poi
    {
        readonly PoiConfig _poiConfig;
        readonly Ork _ork;
        readonly string _variableKey;

        public Poi(PoiConfig poiConfig, SpawnEnvironment environment, MesStaticEncounterConfig[] orkConfigs)
        {
            _poiConfig = poiConfig;
            _ork = new Ork(Id, orkConfigs, environment, poiConfig.Position);
            _variableKey = $"HnzPveSeason.Poi.{Id}";
            CurrentState = PoiState.Occupied;
        }

        public string Id => _poiConfig.Id;
        public Vector3D Position => _poiConfig.Position;
        public PoiState CurrentState { get; private set; }

        public void Load(IMyCubeGrid[] grids) // called once
        {
            _ork.Load(grids);

            PoiBuilder builder;
            if (!MyAPIGateway.Utilities.GetVariable(_variableKey, out builder))
            {
                builder = new PoiBuilder
                {
                    CurrentState = PoiState.Occupied,
                };
            }

            SetState(builder.CurrentState, true);
            _ork.EncounterIndex = builder.OrkEncounterIndex;
        }

        public void Unload() // called once
        {
            _ork.Unload();
        }

        void SetState(PoiState state, bool force = false)
        {
            if (CurrentState == state && !force) return;

            if (CurrentState != state)
            {
                MyLog.Default.Info($"[HnzPveSeason] POI `{Id}` state changed from {CurrentState} to {state}");
            }

            CurrentState = state;
            _ork.SetActiveEncounter(CurrentState == PoiState.Occupied);

            Save();
        }

        void Save()
        {
            var builder = new PoiBuilder
            {
                CurrentState = CurrentState,
                OrkEncounterIndex = _ork.EncounterIndex,
            };

            var xml = MyAPIGateway.Utilities.SerializeToXML(builder);
            MyAPIGateway.Utilities.SetVariable(_variableKey, xml);
        }

        public void Update()
        {
            _ork.Update();
        }

        public void Release()
        {
            SetState(PoiState.Released);
        }
    }
}