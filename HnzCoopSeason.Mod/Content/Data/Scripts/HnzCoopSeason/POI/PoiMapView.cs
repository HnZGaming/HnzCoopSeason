using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Orks;
using HnzUtils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.POI
{
    public sealed class PoiMapView
    {
        public static readonly PoiMapView Instance = new PoiMapView();
        readonly NetworkMessenger _requestMessenger;
        readonly NetworkMessenger _responseMessenger;
        readonly LocalGpsCollection<string> _markers;
        readonly GpsVisibilityStore _visibility; //stored on client storage

        // last payload's snapshot per marker, for edge detection
        readonly Dictionary<string, MarkerSnapshot> _prev = new Dictionary<string, MarkerSnapshot>();
        static readonly HashSet<long> BossGrids = new HashSet<long>(); // server-sent. see PoiOrk.MainGridId
        bool _visibilityLoaded;

        public static bool IsBossGrid(long entityId) => entityId != 0 && BossGrids.Contains(entityId);

        PoiMapView()
        {
            _requestMessenger = new NetworkMessenger("HnzCoopSeason.PoiMapView.Request");
            _responseMessenger = new NetworkMessenger("HnzCoopSeason.PoiMapView.Response");
            _markers = new LocalGpsCollection<string>();
            _visibility = new GpsVisibilityStore();
        }

        public void Load()
        {
            _visibilityLoaded = false;
            _requestMessenger.Load(OnRequestMessageReceived);
            _responseMessenger.Load(OnResponseMessageReceived);
        }

        public void Unload()
        {
            _markers.Clear();
            BossGrids.Clear();
            _prev.Clear();
            _requestMessenger.Unload();
            _responseMessenger.Unload();
        }

        public void FirstUpdate()
        {
            if (!TryLoadVisibility()) return;

            SendRequest();
        }

        public void Update()
        {
            if (!TryLoadVisibility()) return;

            if (MyAPIGateway.Session.GameplayFrameCounter % 60 == 0)
            {
                CapturePlayerToggles();
            }

            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 5) != 0) return;
            SendRequest();
        }

        void CapturePlayerToggles()
        {
            _visibility.CaptureChanges(_markers.Pairs);
        }

        bool TryLoadVisibility()
        {
            if (_visibilityLoaded) return true;
            if (MyAPIGateway.Session.LocalHumanPlayer == null) return false;

            _visibility.Load(); // client-only; the server has no local markers
            _visibilityLoaded = true;
            return true;
        }

        public void OnPoiStateUpdated() // called in server
        {
            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                SendMarkersToClient(p.SteamUserId);
            }
        }

        void SendRequest()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            MyLog.Default.Debug("[HnzCoopSeason] PoiMapView sending request");
            _requestMessenger.SendToServer(Array.Empty<byte>());
        }

        void OnRequestMessageReceived(ulong senderId, byte[] bytes)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);

            SendMarkersToClient(senderId);
        }

        void SendMarkersToClient(ulong steamId)
        {
            var player = GetPlayer(steamId);
            if (player == null)
            {
                MyLog.Default.Error($"[HnzCoopSeason] PoiMapView player not found: {steamId}");
                return;
            }

            var pois = new List<IPoi>();
            foreach (var poi in GetPois(player.GetPosition()).Take(SessionConfig.Instance.ExposedPoiCount))
            {
                pois.Add(poi);
            }

            // invasion
            foreach (var poi in Session.Instance.GetAllPois().Where(p => p.State == PoiState.Invaded))
            {
                pois.Add(poi);
            }

            var markers = new List<Marker>();
            var bossRange = SessionConfig.Instance.EncounterRadius * PoiOrk.BossRangeMultiplier;
            var bossRangeSq = bossRange * bossRange;
            foreach (var poi in pois)
            {
                var ork = (PoiOrk)poi.Observers.FirstOrDefault(o => o is PoiOrk); //todo messy
                var position = poi.GetEntityPosition();
                var markerPosition = poi.GetMarkerPosition();

                var hideOnHud = poi.State != PoiState.Released // a merchant has no boss marker to defer to
                                && ork != null
                                && ork.HasMainGrid
                                && Vector3D.DistanceSquared(player.GetPosition(), position) <= bossRangeSq;

                markers.Add(new Marker(poi.Id, markerPosition, poi.State, ork?.GetProgressLevel() ?? 0, hideOnHud, ork?.MainGridId ?? 0));
            }

            MyLog.Default.Debug("[HnzCoopSeason] PoiMapView sending response");
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new ResponsePayload { Markers = markers });
            _responseMessenger.SendTo(steamId, bytes);
        }

        void OnResponseMessageReceived(ulong senderId, byte[] bytes)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);
            var payload = MyAPIGateway.Utilities.SerializeFromBinary<ResponsePayload>(bytes);

            if (_visibilityLoaded) CapturePlayerToggles();

            UpdateBossGrids(payload.Markers);
            ApplyMarkersVisibility(payload.Markers);
            RemoveAbsentMarkers(payload.Markers);

            foreach (var marker in payload.Markers)
            {
                UpsertMarker(marker);
            }
        }

        // boss EntityId sent from server.
        void UpdateBossGrids(List<Marker> markers)
        {
            BossGrids.Clear();
            foreach (var m in markers)
            {
                if (m.BossGridId != 0) BossGrids.Add(m.BossGridId);
            }
        }

        // server drives marker visibility;
        void ApplyMarkersVisibility(List<Marker> markers)
        {
            foreach (var m in markers)
            {
                MarkerSnapshot prev;
                var hadPrev = _prev.TryGetValue(m.Id, out prev);

                if (m.HideOnHud && !(hadPrev && prev.HideOnHud)) _visibility.SetHidden(m.Id, true); // boss came into range
                else if (!m.HideOnHud && hadPrev && prev.HideOnHud) _visibility.SetHidden(m.Id, false); // boss left range

                if (hadPrev && prev.State != m.State) _visibility.SetHidden(m.Id, false); // poi state change -> force show

                _prev[m.Id] = new MarkerSnapshot(m.HideOnHud, m.State);
            }
        }

        // drop markers
        void RemoveAbsentMarkers(List<Marker> markers)
        {
            var presentIds = new HashSet<string>(markers.Select(m => m.Id));
            _markers.RemoveExceptFor(presentIds);
            _visibility.PruneAbsent(presentIds);

            var stale = _prev.Keys.Where(id => !presentIds.Contains(id)).ToList();
            foreach (var id in stale) _prev.Remove(id);
        }

        void UpsertMarker(Marker marker)
        {
            var showOnHud = _visibility.IsVisible(marker.Id);

            // rebuild the gps: flipping ShowOnHud alone does not re-register the hud marker
            IMyGps current;
            if (_markers.TryGet(marker.Id, out current) && current.ShowOnHud != showOnHud)
            {
                _markers.Remove(marker.Id);
            }

            IMyGps gps;
            if (_markers.TryGet(marker.Id, out gps))
            {
                UpdateGps(gps, marker);
                // note: do not update hash
            }
            else // new gps
            {
                gps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.Zero, showOnHud, false);
                UpdateGps(gps, marker);
                gps.UpdateHash();
                _markers.Add(marker.Id, gps);
            }
        }

        static IEnumerable<IPoi> GetPois(Vector3D origin)
        {
            var foundMerchant = false;
            foreach (var poi in Session.Instance.GetAllPois().OrderBy(p => Vector3D.Distance(p.Position, origin)))
            {
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (poi.State == PoiState.Released)
                {
                    if (foundMerchant) continue;
                    foundMerchant = true;
                }

                yield return poi;
            }
        }

        static void UpdateGps(IMyGps gps, Marker marker)
        {
            var level = $"Lv.{marker.Level}";
            switch (marker.State)
            {
                case PoiState.Occupied: UpdateGps(gps, $"[ORKS] {marker.Id} ({level})", marker.Position, Color.Orange, "Beat the Orks away from our trading hub!"); break;
                case PoiState.Released: UpdateGps(gps, $"[MERCHANT] {marker.Id}", marker.Position, Color.Green, "Our trading hub has been released and in business!"); break;
                case PoiState.Invaded: UpdateGps(gps, $"[ORKS] {marker.Id} ({level})", marker.Position, Color.Orange, "Ork mobs have reclaimed our trading hub... Take it back!"); break;
                default: throw new InvalidOperationException($"invalid poi state: {marker.State}");
            }
        }

        static void UpdateGps(IMyGps gps, string name, Vector3D position, Color color, string description)
        {
            gps.Name = name;
            gps.Coords = position;
            gps.GPSColor = color;
            gps.Description = description;
        }

        static IMyPlayer GetPlayer(ulong steamId)
        {
            // single player
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                return MyAPIGateway.Session.LocalHumanPlayer;
            }

            // server
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            return MyAPIGateway.Players.TryGetIdentityId(playerId);
        }

        // previous payload snapshot per marker, for edge detection
        struct MarkerSnapshot
        {
            public bool HideOnHud;
            public PoiState State;

            public MarkerSnapshot(bool hideOnHud, PoiState state)
            {
                HideOnHud = hideOnHud;
                State = state;
            }
        }

        [ProtoContract]
        sealed class ResponsePayload
        {
            [ProtoMember(1)]
            public List<Marker> Markers = new List<Marker>();
        }

        [ProtoContract]
        sealed class Marker
        {
            [ProtoMember(1)]
            public string Id;

            [ProtoMember(2)]
            public Vector3D Position;

            [ProtoMember(3)]
            public PoiState State;

            [ProtoMember(4)]
            public int Level;

            [ProtoMember(5)]
            public bool HideOnHud; // for marker

            [ProtoMember(6)]
            public long BossGridId; // 0 if not boss

            // ReSharper disable once UnusedMember.Local
            Marker()
            {
            }

            public Marker(string id, Vector3D position, PoiState state, int level, bool hideOnHud, long bossGridId)
            {
                Id = id;
                Position = position;
                State = state;
                Level = level;
                HideOnHud = hideOnHud;
                BossGridId = bossGridId;
            }
        }
    }
}