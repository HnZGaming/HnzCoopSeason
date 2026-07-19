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

        const int DiscoverySeconds = 15;
        const int NearDiscoverySeconds = 3; // already in sight of the boss marker
        const double NearDiscoveryRangeFactor = 2; // x EncounterRadius

        readonly List<DiscoveryGps> _discoveryGpss = new List<DiscoveryGps>();

        PoiMap _poiMap;
        CommandModule _commandModule;
        bool _doneFirstUpdate;
        bool _richHudReady;
        DatapadInserter _dataPadInserter;
        OrksDamageManipulator _orksDamageManipulator;

        public override void LoadData()
        {
            MyLog.Default.Info("[HnzCoopSeason] session loading");
            base.LoadData();
            Instance = this;

            _commandModule = new CommandModule(VRageUtils.StableKey("HnzCoopSeason.CommandModule"), "coop");
            _commandModule.SendMessage += SendMessage;
            _commandModule.Load();
            InitializeCommands();

            MissionScreen.Load(VRageUtils.StableKey(nameof(MissionScreen)));
            PoiMapDebugView.Instance.Load();
            PoiSpectatorCamera.Instance.Load();
            PoiMapView.Instance.Load();
            MissionService.Instance.Load();
            CoopGridTakeover.Instance.Load();

            // server or single player
            if (VRageUtils.NetworkTypeIn(NetworkType.DediServer | NetworkType.SinglePlayer))
            {
                _poiMap = new PoiMap();

                MESApi.Load();
                PlanetCollection.Load();
                PoiRandomInvasion.Instance.Load();
                RevengeOrkManager.Instance.Load();

                _dataPadInserter = new DatapadInserter("COOP");
                _dataPadInserter.Load(TryCreateDatapadData);

                _orksDamageManipulator = new OrksDamageManipulator("PORKS");
            }

            // client
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                MyLog.Default.Info("[HnzCoopSeason] RichHudClient.Init()");
                RichHudClient.Init(nameof(HnzCoopSeason), RichHudInit, RichHudClosed);
                NpcHud.Instance.Load();
                WcHudApi.Load();
            }

            ProgressionView.Instance.Load();

            MyLog.Default.Info("[HnzCoopSeason] session loaded");
        }

        void RichHudInit() // client
        {
            MyLog.Default.Info("[HnzCoopSeason] RichHudClient.Init() callback");
            CoopHud.Load();
            MissionWindow.Load();
            _richHudReady = true;
        }

        protected override void UnloadData()
        {
            MyLog.Default.Info("[HnzCoopSeason] session unloading");
            base.UnloadData();

            _commandModule.SendMessage -= SendMessage;
            _commandModule.Unload();
            PoiMapDebugView.Instance.Unload();
            MissionScreen.Unload();
            PoiSpectatorCamera.Instance.Unload();
            PoiMapView.Instance.Unload();
            MissionService.Instance.Unload();
            CoopGridTakeover.Instance.Unload();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                MESApi.Unload();
                PlanetCollection.Unload();
                _poiMap.Unload();
                OnlineCharacterCollection.Unload();
                _dataPadInserter?.Unload();
                PoiRandomInvasion.Instance.Unload();
                RevengeOrkManager.Instance.Unload();
                OrkDamageReductionScales.Clear();
            }

            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                NpcHud.Instance.Unload();
                WcHudApi.Unload();
            }

            ProgressionView.Instance.Unload();

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                ScreenTopHud.Instance.Close();
            }

            MyLog.Default.Info("[HnzCoopSeason] session unloaded");
        }

        void RichHudClosed() // client
        {
            _richHudReady = false;
            MissionWindow.Instance.Unload();
            CoopHud.Unload();
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
            // server or single player
            if (VRageUtils.NetworkTypeIn(NetworkType.DediServer | NetworkType.SinglePlayer))
            {
                LoadConfig();

                _orksDamageManipulator.OnFirstFrame();
            }

            // dedi client
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient))
            {
                ProgressionView.Instance.RequestUpdate();
                MissionService.Instance.RequestUpdate();
            }

            CoopGridTakeover.Instance.FirstUpdate();
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
                _orksDamageManipulator.OnEveryFrame();
                DiscardExpiredDiscoveryGps();
            }

            // client or single player
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                if (_richHudReady)
                {
                    NpcHud.Instance.Update();
                    ProgressionView.Instance.UpdateClient();
                    MissionWindow.Instance.Update();
                }
            }

            MissionService.Instance.Update();
            PoiMapView.Instance.Update();
            CoopGridTakeover.Instance.Update();
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

        // how far progress has crossed the current level's span, 0..1
        // e.g. level 5 of 5 spans progress 80%..100%; progress 90% -> 0.5
        public float GetProgressLevelFraction()
        {
            return GetProgressLevelFraction(GetProgressLevel());
        }

        // same, against an arbitrary level's span; a past level clamps to 1, a future one to 0
        public float GetProgressLevelFraction(int level)
        {
            var max = SessionConfig.Instance.MaxProgressLevel;
            var t = GetProgress() * max - (level - 1);
            return MathHelper.Clamp(t, 0f, 1f);
        }

        public bool SetPoiState(string poiId, PoiState state, bool invokeCallbacks = true)
        {
            Poi poi;
            if (!_poiMap.TryGetPoi(poiId, out poi)) return false;
            if (state == PoiState.Invaded && poi.State != PoiState.Released) return false;
            if (!poi.SetState(state)) return false;
            if (!invokeCallbacks) return true;

            MyLog.Default.Info(
                "[HnzCoopSeason] poi state changed: {0}, {1} / {2}, progress: {3:0.0}%, level: {4}",
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
            // discovery gps per player; duration by range, near ones clear fast
            var nearRange = SessionConfig.Instance.EncounterRadius * NearDiscoveryRangeFactor;
            var now = MyAPIGateway.Session.ElapsedPlayTime;

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var player in players)
            {
                var character = player.Character;
                var near = character != null &&
                           Vector3D.DistanceSquared(character.GetPosition(), position) <= nearRange * nearRange;
                var seconds = near ? NearDiscoverySeconds : DiscoverySeconds;

                MyVisualScriptLogicProvider.ShowNotification("Someone just discovered something!", seconds * 1000, "White", player.IdentityId);

                var gps = MyAPIGateway.Session.GPS.Create($"{name} Discovery", "", position, true, true);
                gps.GPSColor = Color.Orange;
                MyAPIGateway.Session.GPS.AddGps(player.IdentityId, gps);

                // DiscardAt is only swept on world load/save, so expire it ourselves
                _discoveryGpss.Add(new DiscoveryGps
                {
                    IdentityId = player.IdentityId,
                    Gps = gps,
                    ExpiresAt = now + TimeSpan.FromSeconds(seconds),
                });
            }
        }

        struct DiscoveryGps
        {
            public long IdentityId;
            public IMyGps Gps;
            public TimeSpan ExpiresAt;
        }

        void DiscardExpiredDiscoveryGps()
        {
            if (_discoveryGpss.Count == 0) return;

            var now = MyAPIGateway.Session.ElapsedPlayTime;
            for (var i = _discoveryGpss.Count - 1; i >= 0; i--)
            {
                var entry = _discoveryGpss[i];
                if (now < entry.ExpiresAt) continue;

                MyAPIGateway.Session.GPS.RemoveGps(entry.IdentityId, entry.Gps);
                _discoveryGpss.RemoveAt(i);
            }
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

        bool TryCreateDatapadData(IMyCubeGrid grid, out string data)
        {
            var closestPoi = GetAllPois()
                .Where(p => p.IsPlanetary)
                .OrderBy(p => Vector3D.Distance(p.Position, grid.GetPosition()))
                .FirstOrDefault();

            if (closestPoi == null)
            {
                MyLog.Default.Warning("[HnzCoopSeason] POI not found for datapad");
                data = null;
                return false;
            }

            var gps = VRageUtils.FormatGps("Something", closestPoi.Position, "FFFFFF");
            data = string.Format(SessionConfig.Instance.RespawnDatapadTextFormat, gps);
            return true;
        }

        public override string ToString()
        {
            return $"Session(progress: {GetProgress()}, progressLevel: {GetProgressLevel()}, {nameof(_poiMap)}: {_poiMap})";
        }
    }
}