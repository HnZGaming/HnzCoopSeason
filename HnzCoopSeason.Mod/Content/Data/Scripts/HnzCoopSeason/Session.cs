using System;
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

        public override void LoadData()
        {
            base.LoadData();
            Instance = this;

            MyLog.Default.Info("[HnzCoopSeason] session loading");

            _commandModule = new CommandModule("coop");
            _commandModule.Load();
            InitializeCommands();

            MissionScreenView.Load();
            PoiGpsView.Load();
            PoiSpectatorCamera.Load();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                _poiMap = new PoiMap();

                MESApi.Load();
                FlashGpsApi.Load(nameof(HnzCoopSeason).GetHashCode());
                PlanetCollection.Load();
                RespawnPodManipulator.Load();
            }

            ProgressionView.Load();

            MyLog.Default.Info("[HnzCoopSeason] session loaded");
        }

        protected override void UnloadData()
        {
            base.UnloadData();

            _commandModule.Unload();
            PoiGpsView.Unload();
            MissionScreenView.Unload();
            PoiSpectatorCamera.Unload();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                MESApi.Unload();
                PlanetCollection.Unload();
                _poiMap.Unload();
                OnlineCharacterCollection.Unload();
                RespawnPodManipulator.Unload();
            }

            ProgressionView.Unload();
        }

        void LoadConfig()
        {
            SessionConfig.Load();
            _poiMap.LoadConfig();
            ProgressionView.UpdateProgress();
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
                ProgressionView.RequestUpdate();
            }
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
        }

        public float GetProgress()
        {
            return _poiMap.GetProgress();
        }

        public bool SetPoiState(string poiId, PoiState state)
        {
            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi)) return false;
            if (!poi.SetState(state)) return false;

            ProgressionView.UpdateProgress();
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
            MyVisualScriptLogicProvider.AddGPS("Something", "", position, Color.Red, 10);
        }

        public static void SendMessage(ulong steamId, Color color, string message)
        {
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            MyVisualScriptLogicProvider.SendChatMessageColored(message, color, "COOP", playerId);
        }

        public bool TryGetClosestPoiPosition(Vector3D position, out Vector3D closestPosition)
        {
            return _poiMap.TryGetClosestPosition(position, out closestPosition);
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
    }
}