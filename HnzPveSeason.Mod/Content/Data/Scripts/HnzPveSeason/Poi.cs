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
        readonly Ork _ork;

        public Poi(PoiConfig poiConfig, MesStaticEncounterConfig[] orkConfigs)
        {
            _poiConfig = poiConfig;
            _ork = new Ork(Id, orkConfigs, poiConfig.Position);
            CurrentState = PoiState.Occupied;
        }

        public string Id => _poiConfig.Id;
        public Vector3D Position => _poiConfig.Position;
        public PoiState CurrentState { get; private set; }

        public void Load(IMyCubeGrid[] grids) // called once
        {
            _ork.Load(grids);

            PoiBuilder builder;
            if (PoiBuilder.TryLoad(Id, out builder))
            {
                MyLog.Default.Info($"[HnzPveSeason] poi `{Id}` recovered");
            }
            else
            {
                builder = new PoiBuilder
                {
                    CurrentState = PoiState.Occupied,
                };
            }

            SetState(builder.CurrentState, true);
        }

        public void Unload() // called once
        {
            _ork.Unload();
        }

        void SetState(PoiState state, bool init = false)
        {
            if (CurrentState == state)
            {
                if (!init) return;
            }

            if (!init)
            {
                MyLog.Default.Info($"[HnzPveSeason] POI `{Id}` state changed from {CurrentState} to {state}");
            }

            CurrentState = state;
            _ork.SetActiveEncounter(CurrentState == PoiState.Occupied);

            if (!init)
            {
                Save();
            }
        }

        void Save()
        {
            var builder = new PoiBuilder
            {
                CurrentState = CurrentState,
            };

            PoiBuilder.Save(Id, builder);
            MyLog.Default.Info($"[HnzPveSeason] poi `{Id}` saved");
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