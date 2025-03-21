﻿using System;
using System.Collections.Generic;
using System.Linq;
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
        readonly PoiOrkConfig[] _configs;

        public PoiOrk(string poiId, Vector3D position, PoiOrkConfig[] configs)
        {
            _configs = configs;
            _poiId = poiId;
            _encounter = new MesEncounter($"{poiId}-ork", position);
            _randomInvasionInterval = new Interval();
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnMainGridSet += OnMainGridSet;
            _encounter.OnMainGridUnset += OnMainGridUnset;
            _encounter.SpawnDelegate = EncounterSpawnDelegate;
            _encounter.Load(grids);

            _randomInvasionInterval.Initialize();
        }

        void IPoiObserver.Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
            _encounter.OnMainGridSet -= OnMainGridSet;
            _encounter.OnMainGridUnset -= OnMainGridUnset;
            _encounter.SpawnDelegate = null;
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

        // called upon encounter spawn
        bool EncounterSpawnDelegate(int playerCount, List<string> spawnGroupNames)
        {
            var progressLevel = Session.Instance.GetProgressLevel();
            var minPlayerCount = SessionConfig.Instance.ProgressionLevels[progressLevel].MinPlayerCount;
            if (playerCount < minPlayerCount) return false;

            var configIndex = CalcConfigIndex();
            MyLog.Default.Info($"[HnzCoopSeason] poi ork {_poiId} requesting spawn; index: {configIndex}");

            var config = _configs[configIndex];
            spawnGroupNames.AddRange(config.SpawnGroupNames);
            return true;
        }

        public void Spawn(int configIndex)
        {
            var config = _configs[configIndex];
            _encounter.ForceSpawn(config.SpawnGroupNames);
        }

        int CalcConfigIndex()
        {
            if (_configs.Length == 1) return 0;

            var progressLevel = Session.Instance.GetProgressLevel();
            var weights = _configs.Select(c => GetWeight(c, progressLevel)).ToArray();
            if (weights.Length == 0)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] poi ork {_poiId} no configs eligible; selecting 0");
                return 0;
            }

            return MathUtils.WeightedRandom(weights);
        }

        static float GetWeight(PoiOrkConfig config, int progressLevel)
        {
            if (progressLevel != config.ProgressLevel) return 0;
            return config.Weight;
        }

        public override string ToString()
        {
            return $"Ork({nameof(_poiId)}: {_poiId},  {nameof(_encounter)}: {_encounter})";
        }
    }
}