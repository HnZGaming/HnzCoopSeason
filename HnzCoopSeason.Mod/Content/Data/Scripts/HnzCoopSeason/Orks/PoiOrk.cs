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
        public const int BossRangeMultiplier = 3; // times of a normal encounter radius
        static float BossGpsRange => (float)(SessionConfig.Instance.EncounterRadius * BossRangeMultiplier);

        readonly string _poiId;
        readonly MesEncounter _encounter;
        readonly PoiOrkConfig[] _configs;
        IMyCubeGrid _mainGrid;
        PoiState _poiState;
        bool _disarmNextSpawn;
        // two 16-bit StableKeys packed into 32 bits; GetHashCode() reseeds per process on .NET 10
        long BossGpsId => ((long)VRageUtils.StableKey(nameof(PoiOrk)) << 16) | VRageUtils.StableKey($"boss-{_poiId}");

        public bool HasMainGrid => _mainGrid != null && !_mainGrid.Closed;
        public long MainGridId => HasMainGrid ? _mainGrid.EntityId : 0;

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

        void UpdateBossGps()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 1) != 0) return;
            if (!HasMainGrid) return;

            // a working antenna already marks it on the HUD, so skip the FlashGps
            if (HasBroadcastingAntenna()) return;

            FlashGpsApi.Send(new FlashGpsApi.Entry
            {
                Id = BossGpsId,
                Name = "ORK BOSS",
                Position = _mainGrid.GetPosition(),
                Color = new Color(237, 0, 211), // #ED00D3, matching TargetReticle's boss brackets
                Duration = 3,
                Radius = BossGpsRange,
                EntityId = _mainGrid.EntityId, // client live-tracks the grid; RemoveBossGps drops it on despawn
                Mute = true,
            });
        }

        void RemoveBossGps()
        {
            FlashGpsApi.Send(new FlashGpsApi.Entry
            {
                Id = BossGpsId,
                Name = "ORK BOSS",
                Duration = 0,
                Radius = 0,
                Mute = true,
                Position = Vector3.Zero
            });
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _poiState = state;

            _encounter.SetActive(
                state == PoiState.Occupied ||
                state == PoiState.Invaded);
        }

        bool IPoiObserver.TryGetPosition(out Vector3D position)
        {
            var hasOrkState = _poiState == PoiState.Occupied || _poiState == PoiState.Invaded;
            if (hasOrkState && HasMainGrid)
            {
                position = _mainGrid.GetPosition();
                return true;
            }

            position = Vector3D.Zero;
            return false;
        }

        bool IPoiObserver.TryGetMarkerPosition(out Vector3D position) => ((IPoiObserver)this).TryGetPosition(out position);

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
                if (IsSignalJammer(antenna)) continue;
                antenna.Radius = BossGpsRange;
                antenna.EnableBroadcasting = true;
            }

            Session.Instance.OnOrkDiscovered(_poiId, grid.GetPosition());

            _mainGrid = grid;
            PoiMapView.Instance.OnPoiStateUpdated();
        }

        void OnMainGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} main grid despawn");
            RemoveBossGps();
            _mainGrid = null;
            PoiMapView.Instance.OnPoiStateUpdated();
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

        bool HasBroadcastingAntenna()
        {
            foreach (var antenna in _mainGrid.GetFatBlocks<IMyRadioAntenna>())
            {
                if (IsSignalJammer(antenna)) continue;
                if (!antenna.IsWorking) continue; // destroyed, unpowered or switched off
                if (!antenna.EnableBroadcasting) continue;

                return true;
            }

            return false;
        }

        // MES suppression-field blocks are RadioAntennas with a 'MES-Suppressor-*' subtype
        static bool IsSignalJammer(IMyRadioAntenna antenna) =>
            antenna.BlockDefinition.SubtypeName.IndexOf("Suppressor", StringComparison.OrdinalIgnoreCase) >= 0;

    }
}