using System;
using System.Collections.Generic;
using System.Linq;
using FlashGps;
using GridStorage.API;
using HnzCoopSeason.POI;
using HnzCoopSeason.Spawners;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Orks
{
    public sealed class PoiOrk : IPoiObserver
    {
        readonly string _poiId;
        readonly MesEncounter _encounter;
        readonly PoiOrkConfig[] _configs;
        IMyCubeGrid _mainGrid;
        PoiState _poiState;
        bool _disarmNextSpawn;

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
            _encounter.OnGridSet += OnGridSet;
            _encounter.FilterSpawn = EncounterSpawnDelegate;
            _encounter.Load(grids);

            CoopGridTakeover.Instance.OnTakeoverStateChanged += OnAnyTakeoverStateChanged;
        }

        void IPoiObserver.Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
            _encounter.OnMainGridSet -= OnMainGridSet;
            _encounter.OnMainGridUnset -= OnMainGridUnset;
            _encounter.OnGridSet -= OnGridSet;
            _encounter.FilterSpawn = null;

            CoopGridTakeover.Instance.OnTakeoverStateChanged -= OnAnyTakeoverStateChanged;
        }

        void IPoiObserver.Update()
        {
            _encounter.TrySpawn();
            UpdateBossGps();
        }

        /// <summary>A working antenna already marks the boss on everyone's hud.</summary>
        bool HasBroadcastingAntenna()
        {
            foreach (var antenna in _mainGrid.GetFatBlocks<IMyRadioAntenna>())
            {
                if (antenna.BlockDefinition.SubtypeName.IndexOf("Suppressor", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (!antenna.IsWorking) continue; // destroyed, unpowered or switched off
                if (!antenna.EnableBroadcasting) continue;

                return true;
            }

            return false;
        }

        void UpdateBossGps()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 1) != 0) return;
            if (_mainGrid == null) return;

            // fallback only; a broadcasting boss already marks itself
            if (HasBroadcastingAntenna()) return;

            FlashGpsApi.Send(new FlashGpsApi.Entry
            {
                Id = $"{nameof(PoiOrk)}-boss-{_poiId}".GetHashCode(),
                Name = "ORK BOSS",
                Position = _mainGrid.GetPosition(),
                Color = new Color(237, 0, 211), // #ED00D3, matching TargetReticle's boss brackets
                Duration = 3,
                Radius = SessionConfig.Instance.EncounterRadius * 3,
                EntityId = _mainGrid.EntityId,
                Mute = true,
            });
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _poiState = state;

            _encounter.SetActive(
                state == PoiState.Occupied ||
                state == PoiState.Invaded);
        }

        public bool HasMainGrid => _mainGrid != null && !_mainGrid.Closed;

        bool IPoiObserver.TryGetPosition(out Vector3D position)
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

            // match the ORK BOSS gps reach; suppressors aren't broadcasters
            var bossRange = (float)(SessionConfig.Instance.EncounterRadius * 3);
            foreach (var antenna in grid.GetFatBlocks<IMyRadioAntenna>())
            {
                antenna.HudText = $"[BOSS] {grid.CustomName}";

                if (antenna.BlockDefinition.SubtypeName.IndexOf("Suppressor", StringComparison.OrdinalIgnoreCase) >= 0) continue;

                antenna.Radius = bossRange; // clamps to the block's MaxBroadcastRadius
                antenna.EnableBroadcasting = true;
            }

            Session.Instance.OnOrkDiscovered(_poiId, grid.GetPosition());

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

            TakeoverState state;
            if (!CoopGridTakeover.TryLoadTakeoverState(_mainGrid, out state)) return;
            if (!state.CanTakeOver) return;

            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} main grid defeated by players");
            Session.Instance.SetPoiState(_poiId, PoiState.Released);
        }

        // called upon encounter spawn
        bool EncounterSpawnDelegate(int playerCount, List<string> spawnGroupNames)
        {
            _disarmNextSpawn = false; // natural spawns are always armed

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

        public void Spawn(int configIndex, bool disarm = false)
        {
            _disarmNextSpawn = disarm;

            var config = _configs[configIndex];
            _encounter.ForceSpawn(config.SpawnGroupNames);
        }

        void OnGridSet(IMyCubeGrid grid)
        {
            OrkDamageReductionScales.Register(grid, OrkUtils.ComputeOrksDamageReductionScale(GetProgressLevel()));

            if (!_disarmNextSpawn) return;

            var count = OrkUtils.DisarmGrid(grid);
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} grid disarmed: '{grid.CustomName}', blocks: {count}");
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

        public int GetProgressLevel()
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

        static float GetWeight(PoiOrkConfig config, int progressLevel)
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