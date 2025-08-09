using System;
using System.Collections.Generic;
using System.Linq;
using FlashGps;
using GridStorage.API;
using HnzCoopSeason.Orks;
using HnzCoopSeason.Spawners;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.POI.Reclaim
{
    public sealed class PoiOrk
    {
        readonly string _poiId;
        readonly MesEncounter _encounter;
        readonly OrkConfig[] _configs;
        IMyCubeGrid _mainGrid;
        PoiState _poiState;

        public PoiOrk(string poiId, Vector3D position, OrkConfig[] configs)
        {
            _configs = configs;
            _poiId = poiId;
            _encounter = new MesEncounter($"{poiId}-ork", position);
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _encounter.OnMainGridSet += OnMainGridSet;
            _encounter.OnMainGridUnset += OnMainGridUnset;
            _encounter.FilterSpawn = EncounterSpawnDelegate;
            _encounter.Load(grids);

            CoopGridTakeover.Instance.OnTakeoverStateChanged += OnAnyTakeoverStateChanged;
        }

        public void Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
            _encounter.OnMainGridSet -= OnMainGridSet;
            _encounter.OnMainGridUnset -= OnMainGridUnset;
            _encounter.FilterSpawn = null;

            CoopGridTakeover.Instance.OnTakeoverStateChanged -= OnAnyTakeoverStateChanged;
        }

        public void Save()
        {
        }

        public void Update()
        {
            _encounter.TrySpawn();
            UpdateBossGps();
        }

        void UpdateBossGps()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 1) != 0) return;
            if (_mainGrid == null) return;

            FlashGpsApi.Send(new FlashGpsApi.Entry
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

        public void OnStateChanged(PoiState state)
        {
            _poiState = state;

            _encounter.SetActive(
                state == PoiState.Occupied ||
                state == PoiState.Invaded);
        }

        public bool TryGetPosition(out Vector3D position)
        {
            var hasOrkState = _poiState == PoiState.Occupied || _poiState == PoiState.Invaded;
            var hasGrid = _mainGrid != null && !_mainGrid.Closed;
            if (hasOrkState && hasGrid)
            {
                position = _mainGrid.GetPosition();
                return true;
            }

            position = Vector3D.Zero;
            return false;
        }

        void OnMainGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} main grid spawn");

            foreach (var beacon in grid.GetFatBlocks<IMyBeacon>())
            {
                beacon.HudText = $"[BOSS] {grid.CustomName}";
            }

            foreach (var antenna in grid.GetFatBlocks<IMyRadioAntenna>())
            {
                antenna.HudText = $"[BOSS] {grid.CustomName}";
            }

            Session.SendNotification(_poiId.GetHashCode(), "Orks Discovery", Color.Red, grid.GetPosition(), 10, "Orks have been discovered!");

            _mainGrid = grid;
        }

        void OnMainGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} main grid despawn");
            _mainGrid = null;
        }

        void OnAnyTakeoverStateChanged(long gridId)
        {
            if (_poiState == PoiState.Released) return;
            if (_mainGrid == null) return;
            if (_mainGrid.EntityId != gridId) return;

            // note: debug via `/coop poi list` and `/coop poi print` commands
            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(_mainGrid, out state)) return;
            if (!state.CanTakeOver) return;

            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} main grid defeated by players");
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
            if (_poiState == PoiState.Invaded) return 1;

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
            if (_poiState == PoiState.Invaded)
            {
                return Math.Max(1, sessionLevel - 2);
            }

            return sessionLevel;
        }

        long[] GetTakeoverPlayerGroup()
        {
            if (_mainGrid == null) return Array.Empty<long>();

            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(_mainGrid, out state)) return Array.Empty<long>();

            return state.Controllers;
        }

        static float GetWeight(OrkConfig config, int progressLevel)
        {
            if (progressLevel != config.ProgressLevel) return 0;
            return config.Weight;
        }

        public override string ToString()
        {
            var takeover = GetTakeoverPlayerGroup();
            return $"Ork({nameof(_poiId)}: {_poiId},  {nameof(_encounter)}: {_encounter}, Takeover: {takeover.ToStringSeq()})";
        }
    }
}