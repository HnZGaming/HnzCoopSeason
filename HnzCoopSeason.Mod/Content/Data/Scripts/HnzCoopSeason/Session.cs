using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Missions;
using FlashGps;
using HnzCoopSeason.HudUtils;
using HnzCoopSeason.Missions.Hud;
using HnzCoopSeason.NPC;
using HnzCoopSeason.Orks;
using HnzCoopSeason.POI;
using MES;
using HnzUtils;
using HnzUtils.Commands;
using HudAPI;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using RichHudFramework.Client;
using VRage.Game.ModAPI;
using VRage.ModAPI;

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
        HudAPIv2 _api;

        public override void LoadData()
        {
            MyLog.Default.Info("[HnzUtils] session loading");
            base.LoadData();
            Instance = this;

            _api = new HudAPIv2();

            _commandModule = new CommandModule((ushort)"HnzCoopSeason.CommandModule".GetHashCode(), "coop");
            _commandModule.SendMessage += SendMessage;
            _commandModule.Load();
            InitializeCommands();

            MissionScreen.Load((ushort)nameof(MissionScreen).GetHashCode());
            PoiMapDebugView.Instance.Load();
            PoiSpectatorCamera.Instance.Load();
            PoiMapView.Instance.Load();
            MissionService.Instance.Load();

            // server
            if (VRageUtils.NetworkTypeIn(NetworkType.DediServer | NetworkType.SinglePlayer))
            {
                _poiMap = new PoiMap();

                MESApi.Load();
                PlanetCollection.Load();
                RespawnPodManipulator.Load();
                PoiRandomInvasion.Instance.Load();
                RevengeOrkManager.Instance.Load();
            }

            // client
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                RichHudClient.Init(nameof(HnzCoopSeason), RichHudInit, RichHudClosed);
                MissionClient.Instance.Load();
            }

            ProgressionView.Instance.Load();
            NpcHud.Instance.Load();

            MyLog.Default.Info("[HnzUtils] session loaded");
        }

        void RichHudInit() // client
        {
            MissionWindow.Load();
        }

        protected override void UnloadData()
        {
            MyLog.Default.Info("[HnzUtils] session unloading");
            base.UnloadData();

            _api = null;

            _commandModule.SendMessage -= SendMessage;
            _commandModule.Unload();
            PoiMapDebugView.Instance.Unload();
            MissionScreen.Unload();
            PoiSpectatorCamera.Instance.Unload();
            PoiMapView.Instance.Unload();
            MissionService.Instance.Unload();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                MESApi.Unload();
                PlanetCollection.Unload();
                _poiMap.Unload();
                OnlineCharacterCollection.Unload();
                RespawnPodManipulator.Unload();
                PoiRandomInvasion.Instance.Unload();
                RevengeOrkManager.Instance.Unload();
            }

            ProgressionView.Instance.Unload();
            NpcHud.Instance.Unload();

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                ScreenTopHud.Instance.Close();
            }

            // client
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                MissionClient.Instance.Unload();
            }

            MyLog.Default.Info("[HnzUtils] session unloaded");
        }

        void RichHudClosed() // client
        {
            MissionWindow.Instance.Unload();
        }

        void LoadConfig() //server
        {
            // collect all grids in the scene
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            var grids = entities.OfType<IMyCubeGrid>().ToArray();

            SessionConfig.Load();
            _poiMap.LoadConfig(grids);
            ProgressionView.Instance.UpdateProgress();
            MissionService.Instance.UpdateMissions();
        }

        void FirstUpdate()
        {
            if (VRageUtils.NetworkTypeIn(NetworkType.DediServer | NetworkType.SinglePlayer))
            {
                LoadConfig();
            }

            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient))
            {
                ProgressionView.Instance.RequestUpdate();
                MissionClient.Instance.RequestUpdate();
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
                PoiRandomInvasion.Instance.Update();
            }

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                if (_api.Heartbeat)
                {
                    NpcHud.Instance.Update();
                    ScreenTopHud.Instance.Render();
                    MissionClient.Instance.Update();
                }
            }

            PoiMapView.Instance.Update();
        }

        public float GetProgress()
        {
            var allPoiCount = _poiMap.AllPois.Count;
            if (allPoiCount == 0) return 0;

            return _poiMap.GetPoiCountByState(PoiState.Released) / (float)allPoiCount;
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
            if (state == PoiState.Invaded && poi.State != PoiState.Released) return false;
            if (!poi.SetState(state)) return false;
            if (!invokeCallbacks) return true;

            MyLog.Default.Info(
                "[HnzUtils] poi state changed: {0}, {1} / {2}, progress: {3:0.0}%, level: {4}",
                poiId,
                _poiMap.GetPoiCountByState(PoiState.Released),
                _poiMap.AllPois.Count,
                GetProgress() * 100,
                GetProgressLevel());

            ProgressionView.Instance.UpdateProgress();
            PoiMapView.Instance.OnPoiStateUpdated(); // gps hud
            MissionService.Instance.UpdateMissions();

            if (state == PoiState.Released)
            {
                OnPoiReleased(poiId, poi.Position);
            }

            if (state == PoiState.Invaded)
            {
                OnPoiInvaded(poiId, poi.Position);
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
            FlashGpsApi.Send(new FlashGpsApi.Entry
            {
                Id = "POI Discovery".GetHashCode(),
                Name = $"{name} Discovery",
                Position = position,
                Color = Color.Orange,
                Duration = 10,
            });
        }

        void OnPoiReleased(string poiId, Vector3D position)
        {
            MyVisualScriptLogicProvider.ShowNotificationToAll("Orks have been defeated!", 10000);
            FlashGpsApi.Send(new FlashGpsApi.Entry
            {
                Id = "POI Release".GetHashCode(),
                Name = "Orks Defeated",
                Position = position,
                Color = Color.Green,
                Duration = 10,
            });
        }

        void OnPoiInvaded(string poiId, Vector3D position)
        {
            MyVisualScriptLogicProvider.ShowNotificationToAll("Orks have came back to our trading hub!", 10 * 1000);
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