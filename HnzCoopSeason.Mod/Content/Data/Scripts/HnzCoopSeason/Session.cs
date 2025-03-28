﻿using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.FlashGPS;
using HnzCoopSeason.MES;
using HnzCoopSeason.Utils;
using HnzCoopSeason.Utils.Commands;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed partial class Session : MySessionComponentBase
    {
        public static Session Instance { get; private set; }

        PoiMap _poiMap;
        CommandModule _commandModule;
        bool _doneFirstUpdate;
        ProgressionView _progressionView;

        public override void LoadData()
        {
            MyLog.Default.Info("[HnzCoopSeason] session loading");
            base.LoadData();
            Instance = this;

            _commandModule = new CommandModule("coop");
            _commandModule.Load();
            InitializeCommands();

            MissionScreenView.Load();
            PoiMapDebugView.Load();
            PoiSpectatorCamera.Load();
            PoiMapView.Instance.Load();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                _poiMap = new PoiMap();

                MESApi.Load();
                FlashGpsApi.Load(nameof(HnzCoopSeason).GetHashCode());
                PlanetCollection.Load();
                RespawnPodManipulator.Load();
            }

            _progressionView = new ProgressionView();
            _progressionView.Load();

            MyLog.Default.Info("[HnzCoopSeason] session loaded");
        }

        protected override void UnloadData()
        {
            MyLog.Default.Info("[HnzCoopSeason] session unloading");
            base.UnloadData();

            _commandModule.Unload();
            PoiMapDebugView.Unload();
            MissionScreenView.Unload();
            PoiSpectatorCamera.Unload();
            PoiMapView.Instance.Unload();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                MESApi.Unload();
                PlanetCollection.Unload();
                _poiMap.Unload();
                OnlineCharacterCollection.Unload();
                RespawnPodManipulator.Unload();
            }

            _progressionView.Unload();

            MyLog.Default.Info("[HnzCoopSeason] session unloaded");
        }

        void LoadConfig()
        {
            SessionConfig.Load();
            _poiMap.LoadConfig();
            _progressionView.UpdateProgress();
        }

        void FirstUpdate()
        {
            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                LoadConfig();
            }

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _progressionView.RequestUpdate();
            }

            PoiMapView.Instance.FirstUpdate();
        }

        public override void UpdateBeforeSimulation()
        {
            base.UpdateBeforeSimulation();

            if (!_doneFirstUpdate)
            {
                _doneFirstUpdate = true;
                FirstUpdate();
            }

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                OnlineCharacterCollection.Update();
                _poiMap.Update();
            }

            PoiMapView.Instance.Update();
        }

        public float GetProgress()
        {
            return _poiMap.GetProgress();
        }

        // min: 1
        // max: SessionConfig.Instance.MaxProgressLevel
        public int GetProgressLevel()
        {
            var progress = GetProgress();
            var max = SessionConfig.Instance.MaxProgressLevel;
            return Math.Min((int)Math.Floor(progress * max) + 1, max);
        }

        public bool TryGetPoiState(string poiId, out PoiState state)
        {
            state = PoiState.Occupied;
            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi)) return false;
            state = poi.State;
            return true;
        }

        public bool SetPoiState(string poiId, PoiState state, bool invokeCallbacks = true)
        {
            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi)) return false;
            if (!poi.SetState(state)) return false;
            if (!invokeCallbacks) return true;

            // potentially overwrites some poi's state
            _poiMap.OnPoiStateChanged();

            MyLog.Default.Info(
                "[HnzCoopSeason] poi state changed: {0}, {1} / {2}, progress: {3:0.0}%, level: {4}",
                poiId,
                _poiMap.GetReleasedPoiCount(),
                _poiMap.AllPois.Count,
                GetProgress() * 100,
                GetProgressLevel());

            _progressionView.UpdateProgress();
            PoiMapView.Instance.OnPoiStateUpdated(); // gps hud

            if (state == PoiState.Released)
            {
                OnPoiReleased(poiId, poi.Position);
            }

            return true;
        }

        public bool IsPlayerAroundPoi(string poiId, float radius)
        {
            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi)) return false;

            return poi.IsPlayerAround(radius);
        }

        public void OnMerchantDiscovered(string poiId, Vector3D position)
        {
            OnPoiDiscovered("Merchant", position);
        }

        public void OnOrkDiscovered(string poiId, Vector3D position)
        {
            OnPoiDiscovered("Ork", position);
        }

        void OnPoiDiscovered(string name, Vector3D position)
        {
            MyVisualScriptLogicProvider.ShowNotificationToAll("Someone just discovered something!", 10000);
            MyVisualScriptLogicProvider.AddGPS($"{name} Discovery", "", position, Color.Red, 10);
        }

        public void OnRandomInvasion(string poiId, Vector3D position)
        {
            MyVisualScriptLogicProvider.ShowNotificationToAll("Orks have taken over our land!", 10000);
            MyVisualScriptLogicProvider.AddGPS("Invasion", "", position, Color.Red, 10);
        }

        void OnPoiReleased(string poiId, Vector3D position)
        {
            MyVisualScriptLogicProvider.ShowNotificationToAll("Orks have been defeated!", 10000);
            MyVisualScriptLogicProvider.AddGPS("Orks Defeated", "", position, Color.Green, 10);
        }

        public static void SendMessage(ulong steamId, Color color, string message)
        {
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            MyVisualScriptLogicProvider.SendChatMessageColored(message, color, "COOP", playerId);
        }

        public IEnumerable<IPoi> GetAllPois()
        {
            return _poiMap.AllPois;
        }

        public bool TryGetPoiPosition(string poiId, out Vector3D position)
        {
            Poi poi;
            if (_poiMap.TryGetPoi(poiId, out poi))
            {
                position = poi.Position;
                return true;
            }

            position = Vector3D.Zero;
            return false;
        }

        public override string ToString()
        {
            return $"Session(progress: {GetProgress()}, progressLevel: {GetProgressLevel()}, {nameof(_poiMap)}: {_poiMap})";
        }
    }
}