using System;
using System.Collections.Generic;
using System.Linq;
using FlashGps;
using HnzCoopSeason.HudUtils;
using HnzCoopSeason.Merchants;
using HnzCoopSeason.NPC;
using HnzCoopSeason.Orks;
using HnzCoopSeason.POI;
using HnzCoopSeason.POI.Reclaim;
using MES;
using HnzUtils;
using HudAPI;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace HnzCoopSeason
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed partial class Session : MySessionComponentBase
    {
        public static Session Instance { get; private set; }

        Dictionary<string, IPoi> _allPois;
        bool _doneFirstUpdate;
        HudAPIv2 _richHudApi;
        DatapadInserter _dataPadInserter;

        public override void LoadData()
        {
            MyLog.Default.Info("[HnzUtils] session loading");
            base.LoadData();
            Instance = this;

            _richHudApi = new HudAPIv2();

            LoadCommands();

            MissionScreen.Load((ushort)nameof(MissionScreen).GetHashCode());
            PoiMapDebugView.Instance.Load();
            PoiSpectatorCamera.Instance.Load();
            PoiMapView.Instance.Load();
            CoopGridTakeover.Instance.Load();

            // server or single player
            if (VRageUtils.NetworkTypeIn(NetworkType.DediServer | NetworkType.SinglePlayer))
            {
                _allPois = new Dictionary<string, IPoi>();

                MESApi.Load();
                PlanetCollection.Load();
                MerchantEconomy.Instance.Load();
                PoiRandomInvasion.Instance.Load();
                RevengeOrkManager.Instance.Load();

                _dataPadInserter = new DatapadInserter("COOP");
                _dataPadInserter.Load(TryCreateDatapadData);
            }

            // client
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                MyLog.Default.Info("[HnzCoopSeason] RichHudClient.Init()");
                NpcHud.Instance.Load();
            }

            ProgressionView.Instance.Load();

            MyLog.Default.Info("[HnzUtils] session loaded");
        }

        protected override void UnloadData()
        {
            MyLog.Default.Info("[HnzUtils] session unloading");
            base.UnloadData();

            _richHudApi = null;

            UnloadCommands();

            PoiMapDebugView.Instance.Unload();
            MissionScreen.Unload();
            PoiSpectatorCamera.Instance.Unload();
            PoiMapView.Instance.Unload();
            CoopGridTakeover.Instance.Unload();

            // server or single player
            if (MyAPIGateway.Session.IsServer)
            {
                MESApi.Unload();
                PlanetCollection.Unload();
                MerchantEconomy.Instance.Unload();

                foreach (var p in _allPois) p.Value.Unload(true);
                _allPois.Clear();

                OnlineCharacterCollection.Unload();
                _dataPadInserter?.Unload();
                PoiRandomInvasion.Instance.Unload();
                RevengeOrkManager.Instance.Unload();
                NpcHud.Instance.Unload();
            }

            ProgressionView.Instance.Unload();

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                ScreenTopHud.Instance.Close();
            }

            MyLog.Default.Info("[HnzUtils] session unloaded");
        }

        public override void SaveData()
        {
            base.SaveData();
            foreach (var p in _allPois) p.Value.Save();
        }

        void LoadConfig() //server
        {
            // collect all grids in the scene
            var entities = new HashSet<IMyEntity>();
            MyAPIGateway.Entities.GetEntities(entities);
            var grids = entities.OfType<IMyCubeGrid>().ToArray();

            SessionConfig.Load();
            LoadPois(grids);
            ProgressionView.Instance.UpdateProgress();
        }

        void LoadPois(IMyCubeGrid[] sceneGrids)
        {
            foreach (var p in _allPois.Values) p.Unload(false);
            _allPois.Clear();

            var poiFactory = new PoiFactory();
            if (!poiFactory.TryLoad()) return;

            // space POIs
            var poiCountPerAxis = SessionConfig.Instance.PoiCountPerAxis;
            var mapOrigin = SessionConfig.Instance.PoiMapCenter;
            var mapRadius = SessionConfig.Instance.PoiMapRadius;
            foreach (var p in MathUtils.Range3D(poiCountPerAxis))
            {
                var position = mapOrigin + (p / poiCountPerAxis * 2 - Vector3D.One) * mapRadius;
                if (Vector3D.Distance(position, mapOrigin) > mapRadius) continue; // circular shape

                PoiReclaim poi;
                if (poiFactory.TryCreateSpacePoi($"{p.X}-{p.Y}-{p.Z}", position, out poi))
                {
                    _allPois.Add(poi.Id, poi);
                }
            }

            // planetary POIs
            foreach (var config in SessionConfig.Instance.PlanetaryPois)
            {
                PoiReclaim poi;
                if (poiFactory.TryCreatePlanetaryPoi(config, out poi))
                {
                    _allPois.Add(config.Id, poi);
                }
            }

            foreach (var p in _allPois.Values) p.Load(sceneGrids);
            MyLog.Default.Info($"[HnzCoopSeason] POIs loaded: {_allPois.Keys.ToStringSeq()}");
        }

        void FirstUpdate()
        {
            // server or single player
            if (VRageUtils.NetworkTypeIn(NetworkType.DediServer | NetworkType.SinglePlayer))
            {
                LoadConfig();
            }

            // dedi client
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient))
            {
                ProgressionView.Instance.RequestUpdate();
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
                MerchantEconomy.Instance.Update();

                foreach (var p in _allPois) p.Value.Update();

                PoiRandomInvasion.Instance.Update();
            }

            // client or single player
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                if (_richHudApi.Heartbeat)
                {
                    NpcHud.Instance.Update();
                    ScreenTopHud.Instance.Render();
                }
            }

            PoiMapView.Instance.Update();
            CoopGridTakeover.Instance.Update();
        }

        public float GetProgress()
        {
            if (_allPois.Count == 0) return 0;
            return GetPoiCountByState(PoiState.Released) / (float)_allPois.Count;
        }

        // min: 1
        // max: SessionConfig.Instance.MaxProgressLevel
        public int GetProgressLevel()
        {
            var progress = GetProgress();
            var max = SessionConfig.Instance.MaxProgressLevel;
            return Math.Min((int)Math.Floor(progress * max) + 1, max);
        }

        public bool SetPoiState(string poiId, PoiState state, bool invokeCallbacks = true)
        {
            IPoi poi;
            if (!_allPois.TryGetValue(poiId, out poi)) return false;
            if (state == PoiState.Invaded && poi.State != PoiState.Released) return false;
            if (!poi.TrySetState(state)) return false;
            if (!invokeCallbacks) return true;

            MyLog.Default.Info(
                "[HnzUtils] poi state changed: {0}, {1} / {2}, progress: {3:0.0}%, level: {4}",
                poiId,
                GetPoiCountByState(PoiState.Released),
                _allPois.Count,
                GetProgress() * 100,
                GetProgressLevel());

            ProgressionView.Instance.UpdateProgress();
            PoiMapView.Instance.OnPoiStateUpdated(); // gps hud

            if (state == PoiState.Released)
            {
                SendNotification("POI Release".GetHashCode(), "Orks Defeated", Color.Green, poi.Position, 10, "Orks have been defeated!");
            }

            if (state == PoiState.Invaded)
            {
                SendNotification("POI Invaded".GetHashCode(), "Orks Invasion", Color.Red, poi.Position, 10, "Orks have came back to our trading hub!");
            }

            return true;
        }

        public IEnumerable<IPoi> GetAllPois()
        {
            return _allPois.Values;
        }

        public bool TryGetPoiPosition(string poiId, out Vector3D position)
        {
            IPoi poi;
            if (_allPois.TryGetValue(poiId, out poi))
            {
                position = poi.Position;
                return true;
            }

            position = Vector3D.Zero;
            return false;
        }

        int GetPoiCountByState(PoiState state)
        {
            return _allPois.Values.Count(poi => poi.State == state);
        }

        bool TryCreateDatapadData(IMyCubeGrid grid, out string data)
        {
            var gp = grid.GetPosition();
            var closestPoi = _allPois.Values
                .Where(p => p.IsPlanetary) // vanilla data pads behavior; no idea why
                .OrderBy(p => Vector3D.DistanceSquared(p.Position, gp))
                .FirstOrDefault();

            if (closestPoi == null)
            {
                MyLog.Default.Warning("[HnzUtils] POI not found for datapad");
                data = null;
                return false;
            }

            var gps = VRageUtils.FormatGps("Something", closestPoi.Position, "FFFFFF");
            data = string.Format(SessionConfig.Instance.RespawnDatapadTextFormat, gps);
            return true;
        }

        public static void SendNotification(long id, string title, Color color, Vector3D position, int duration, string message)
        {
            MyVisualScriptLogicProvider.ShowNotificationToAll(message, duration * 1000);
            FlashGpsApi.Send(new FlashGpsApi.Entry { Id = id, Name = title, Position = position, Color = color, Duration = duration });
        }

        public override string ToString()
        {
            return $"Session(progress: {GetProgress()}, progressLevel: {GetProgressLevel()}, pois: {_allPois.Values.ToStringSeq()})";
        }
    }
}