using System;
using HnzPveSeason.FlashGPS;
using HnzPveSeason.MES;
using HnzPveSeason.Utils;
using HnzPveSeason.Utils.Commands;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
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

            MyLog.Default.Info("[HnzPveSeason] session loading");

            MissionScreenView.Load();

            _commandModule = new CommandModule("pve");
            _commandModule.Load();
            InitializeCommands();

            PoiGpsView.Load();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                _poiMap = new PoiMap();

                MESApi.Load();
                FlashGpsApi.Load(nameof(HnzPveSeason).GetHashCode());
                PlanetCollection.Load();
            }

            ProgressionView.Instance.Load();

            MyLog.Default.Info("[HnzPveSeason] session loaded");
        }

        protected override void UnloadData()
        {
            base.UnloadData();

            _commandModule.Unload();
            PoiGpsView.Unload();
            MissionScreenView.Unload();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                MESApi.Unload();
                PlanetCollection.Unload();
                _poiMap.Unload();
                OnlineCharacterCollection.Unload();
            }

            ProgressionView.Instance.Unload();
        }

        void LoadConfig()
        {
            SessionConfig.Load();
            _poiMap.LoadConfig();
            ProgressionView.Instance.UpdateProgress();
        }

        void FirstUpdate()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                LoadConfig();
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                // client init
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

            OnlineCharacterCollection.Update();

            _poiMap.Update();
        }

        public float GetProgress()
        {
            return _poiMap.GetProgression();
        }

        public bool SetPoiState(string poiId, PoiState state)
        {
            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi)) return false;
            if (!poi.SetState(state)) return false;

            ProgressionView.Instance.UpdateProgress();
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
            MyVisualScriptLogicProvider.SendChatMessageColored(message, color, "pve", playerId);
        }
    }
}