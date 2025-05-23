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
        readonly PoiOrkConfig[] _configs;
        IMyCubeGrid _mainGrid;

        public PoiOrk(string poiId, Vector3D position, PoiOrkConfig[] configs)
        {
            _configs = configs;
            _poiId = poiId;
            _encounter = new MesEncounter($"{poiId}-ork", position);
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnMainGridSet += OnMainGridSet;
            _encounter.OnMainGridUnset += OnMainGridUnset;
            _encounter.SpawnDelegate = EncounterSpawnDelegate;
            _encounter.Load(grids);
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
            _encounter.TrySpawn();
            UpdateBossGps();
        }

        void UpdateBossGps()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 1) != 0) return;
            if (_mainGrid == null) return;

            FlashGps.Instance.Send(new FlashGps.Entry
            {
                Id = $"{nameof(PoiOrk)}-boss-{_poiId}".GetHashCode(),
                Name = "ORK BOSS",
                Position = _mainGrid.GetPosition(),
                Color = Color.Orange,
                Duration = 3,
                Radius = SessionConfig.Instance.EncounterRadius * 3,
                EntityId = _mainGrid.EntityId,
                Mute = true,
            });
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _encounter.SetActive(
                state == PoiState.Occupied ||
                state == PoiState.Invaded);
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

            _mainGrid = grid;
        }

        void OnMainGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} despawn");
            grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;
            _mainGrid = null;
        }

        void OnBlockOwnershipChanged(IMyCubeGrid grid)
        {
            if (grid == null) return; // potential crash cause

            PoiState state;
            if (!Session.Instance.TryGetPoiState(_poiId, out state)) return; // shouldn't happen

            if (state == PoiState.Released) return;
            if (!CoopGrids.IsTakenOver(grid)) return;

            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} defeated by players");
            Session.Instance.SetPoiState(_poiId, PoiState.Released);
        }

        // called upon encounter spawn
        bool EncounterSpawnDelegate(int playerCount, List<string> spawnGroupNames)
        {
            var minPlayerCount = GetMinPlayerCount();
            if (playerCount < minPlayerCount) return false;

            var configIndex = CalcConfigIndex();
            MyLog.Default.Info($"[HnzCoopSeason] poi ork {_poiId} requesting spawn; index: {configIndex}");

            var config = _configs[configIndex];
            spawnGroupNames.AddRange(config.SpawnGroupNames);
            return true;
        }

        int GetMinPlayerCount()
        {
            PoiState state;
            if (Session.Instance.TryGetPoiState(_poiId, out state) && state == PoiState.Invaded) return 1;

            var progressLevel = GetProgressLevel();
            return SessionConfig.Instance.ProgressionLevels[progressLevel].MinPlayerCount;
        }

        public void Spawn(int configIndex)
        {
            var config = _configs[configIndex];
            _encounter.ForceSpawn(config.SpawnGroupNames);
        }

        int CalcConfigIndex()
        {
            if (_configs.Length == 1) return 0;

            var progressLevel = GetProgressLevel();
            var weights = _configs.Select(c => GetWeight(c, progressLevel)).ToArray();
            if (weights.Length == 0)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] poi ork {_poiId} no configs eligible; selecting 0");
                return 0;
            }

            return MathUtils.WeightedRandom(weights);
        }

        int GetProgressLevel()
        {
            var sessionLevel = Session.Instance.GetProgressLevel();

            // invasion
            PoiState state;
            if (Session.Instance.TryGetPoiState(_poiId, out state) && state == PoiState.Invaded)
            {
                return Math.Max(1, sessionLevel - 2);
            }

            return sessionLevel;
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