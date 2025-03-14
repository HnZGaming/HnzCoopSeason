﻿using System;
using HnzCoopSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class PoiOrk : IPoiObserver
    {
        readonly string _poiId;
        readonly MesEncounter _encounter;
        readonly Interval _randomInvasionInterval;
        readonly Vector3D _position;
        PoiState _poiState;

        public PoiOrk(string poiId, Vector3D position, MesEncounterConfig[] configs)
        {
            _position = position;
            _poiId = poiId;
            _encounter = new MesEncounter($"{poiId}-ork", configs, position);
            _randomInvasionInterval = new Interval();
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnMainGridSet += OnMainGridSet;
            _encounter.OnMainGridUnset += OnMainGridUnset;
            _encounter.Load(grids);

            _randomInvasionInterval.Initialize();
        }

        void IPoiObserver.Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
            _encounter.OnMainGridSet -= OnMainGridSet;
            _encounter.OnMainGridUnset -= OnMainGridUnset;
        }

        void IPoiObserver.Update()
        {
            _encounter.Update();
            AttemptRandomInvasion();
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _poiState = state;
            _encounter.SetActive(state == PoiState.Occupied);
        }

        void OnMainGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} spawn");
            grid.OnBlockOwnershipChanged += OnGridOwnershipChanged;
            
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
            grid.OnBlockOwnershipChanged -= OnGridOwnershipChanged;
        }

        void OnGridOwnershipChanged(IMyCubeGrid grid)
        {
            if (!VRageUtils.IsGridControlledByAI(grid))
            {
                MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} defeated by players");
                Session.Instance.SetPoiState(_poiId, PoiState.Released);
            }
        }

        void AttemptRandomInvasion()
        {
            if (_poiState == PoiState.Occupied) return;

            var span = SessionConfig.Instance.RandomInvasionInterval * 60;
            if (!_randomInvasionInterval.Update(span)) return;

            if (Session.Instance.IsPlayerAroundPoi(_poiId, SessionConfig.Instance.EncounterRadius)) return;

            var chance = SessionConfig.Instance.RandomInvasionChance;
            var random = MyRandom.Instance.NextDouble();
            if (random <= chance) return;

            MyLog.Default.Info($"[HnzCoopSeason] ork {_poiId} random invasion");
            Session.Instance.SetPoiState(_poiId, PoiState.Occupied);
            Session.Instance.OnRandomInvasion(_poiId, _position);
        }

        public void Spawn(int configIndex)
        {
            _encounter.ForceSpawn(configIndex);
        }

        public override string ToString()
        {
            return $"Ork({nameof(_poiId)}: {_poiId},  {nameof(_encounter)}: {_encounter})";
        }
    }
}