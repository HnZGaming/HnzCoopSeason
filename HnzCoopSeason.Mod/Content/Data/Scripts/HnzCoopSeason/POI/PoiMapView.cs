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
        readonly GpsVisibilityStore _visibility;
        readonly HashSet<string> _autoHidden = new HashSet<string>(); // hidden by us, not by the player
        readonly HashSet<string> _autoHideOverridden = new HashSet<string>(); // player turned an auto-hidden marker back on
        bool _visibilityLoaded;

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
                if (_autoHidden.Count > 0)
                {
                    foreach (var pair in _markers.Pairs)
                    {
                        if (!pair.Value.ShowOnHud) continue;
                        if (!_autoHidden.Remove(pair.Key)) continue;

                        _autoHideOverridden.Add(pair.Key);
                        MyLog.Default.Info($"[HnzCoopSeason] gps {pair.Key} auto-hide overridden by player");
                    }
                }

                CapturePlayerToggles();
            }

            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 5) != 0) return;
            SendRequest();
        }

        void CapturePlayerToggles()
        {
            _visibility.CaptureChanges(_autoHidden.Count == 0
                ? _markers.Pairs
                : _markers.Pairs.Where(p => !_autoHidden.Contains(p.Key)));
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
            foreach (var poi in pois)
            {
                var ork = (PoiOrk)poi.Observers.FirstOrDefault(o => o is PoiOrk); //todo messy
                var position = poi.GetEntityPosition();

                var bossRange = SessionConfig.Instance.EncounterRadius * 3; // matches PoiOrk's gps radius
                var hideOnHud = ork != null
                                && ork.HasMainGrid
                                && Vector3D.DistanceSquared(player.GetPosition(), position) <= bossRange * bossRange;

                markers.Add(new Marker(poi.Id, position, poi.State, ork?.GetProgressLevel() ?? 0, hideOnHud));
            }

            MyLog.Default.Debug("[HnzCoopSeason] PoiMapView sending response");
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new ResponsePayload { Markers = markers });
            _responseMessenger.SendTo(steamId, bytes);
        }

        void OnResponseMessageReceived(ulong senderId, byte[] bytes)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);
            var payload = MyAPIGateway.Utilities.SerializeFromBinary<ResponsePayload>(bytes);

            // capture before the new states land, or a fresh hide gets stamped with the incoming state
            if (_visibilityLoaded) CapturePlayerToggles();

            var presentIds = new HashSet<string>(payload.Markers.Select(m => m.Id));

            foreach (var m in payload.Markers)
            {
                if (m.HideOnHud)
                {
                    // hide once on the transition; re-asserting would defeat the panel's show toggle
                    if (!_autoHideOverridden.Contains(m.Id) && _autoHidden.Add(m.Id))
                    {
                        MyLog.Default.Info($"[HnzCoopSeason] gps {m.Id} auto-hidden: boss marker in range");
                    }
                }
                else
                {
                    _autoHidden.Remove(m.Id);
                    _autoHideOverridden.Remove(m.Id);
                }
            }

            PruneAbsent(_autoHidden, presentIds);
            PruneAbsent(_autoHideOverridden, presentIds);

            // remove old markers
            _markers.RemoveExceptFor(presentIds);

            _visibility.PruneAbsent(presentIds);

            // add new markers
            foreach (var marker in payload.Markers)
            {
                // rebuild the gps: flipping ShowOnHud alone does not re-register the hud marker
                if (_visibility.ClearIfStateChanged(marker.Id, marker.State))
                {
                    _markers.Remove(marker.Id);
                }

                var showOnHud = _visibility.IsVisible(marker.Id) && !_autoHidden.Contains(marker.Id);

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
                else
                {
                    gps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.Zero, showOnHud, false);
                    UpdateGps(gps, marker);
                    gps.UpdateHash();
                    _markers.Add(marker.Id, gps);
                }
            }
        }

        static void PruneAbsent(HashSet<string> ids, ICollection<string> presentIds)
        {
            if (ids.Count == 0) return;

            List<string> gone = null;
            foreach (var id in ids)
            {
                if (presentIds.Contains(id)) continue;

                if (gone == null) gone = new List<string>();
                gone.Add(id);
            }

            if (gone == null) return;

            foreach (var id in gone)
            {
                ids.Remove(id);
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
            public bool HideOnHud; // boss broadcasts its own marker here

            // ReSharper disable once UnusedMember.Local
            Marker()
            {
            }

            public Marker(string id, Vector3D position, PoiState state, int level, bool hideOnHud)
            {
                Id = id;
                Position = position;
                State = state;
                Level = level;
                HideOnHud = hideOnHud;
            }
        }
    }
}