using System;
using System.Collections.Generic;
using System.Linq;
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

        PoiMapView()
        {
            _requestMessenger = new NetworkMessenger("HnzCoopSeason.PoiMapView.Request");
            _responseMessenger = new NetworkMessenger("HnzCoopSeason.PoiMapView.Response");
            _markers = new LocalGpsCollection<string>();
        }

        public void Load()
        {
            _requestMessenger.Load(OnRequestMessageReceived);
            _responseMessenger.Load(OnResponseMessageReceived);
        }

        public void Unload()
        {
            _requestMessenger.Unload();
            _responseMessenger.Unload();
        }

        public void FirstUpdate()
        {
            if (MyAPIGateway.Session.LocalHumanPlayer == null) return;
            SendRequest();
        }

        public void Update()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % (60 * 5) != 0) return;
            if (MyAPIGateway.Session.LocalHumanPlayer == null) return;
            SendRequest();
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

        void SendRequest() // called in client
        {
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
            IMyPlayer player;
            if (MyAPIGateway.Utilities.IsDedicated) // server
            {
                var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
                player = MyAPIGateway.Players.TryGetIdentityId(playerId);
            }
            else // single player
            {
                player = MyAPIGateway.Session.LocalHumanPlayer;
            }

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
                markers.Add(new Marker(poi.Id, poi.Position, poi.State));
            }

            MyLog.Default.Debug("[HnzCoopSeason] PoiMapView sending response");
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new ResponsePayload { Markers = markers });
            _responseMessenger.SendTo(steamId, bytes);
        }

        void OnResponseMessageReceived(ulong senderId, byte[] bytes)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);
            var payload = MyAPIGateway.Utilities.SerializeFromBinary<ResponsePayload>(bytes);

            // remove old markers
            _markers.RemoveExceptFor(payload.Markers.Select(m => m.Id));

            // add new markers
            foreach (var marker in payload.Markers)
            {
                IMyGps gps;
                if (_markers.TryGet(marker.Id, out gps))
                {
                    UpdateGps(gps, marker);
                    // note: do not update hash
                }
                else // new gps
                {
                    gps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.Zero, true, false);
                    UpdateGps(gps, marker);
                    gps.UpdateHash(); // init hash
                    _markers.Add(marker.Id, gps);
                }
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
            switch (marker.State)
            {
                case PoiState.Occupied: UpdateGps(gps, "Trading Hub [Orks]", marker.Position, Color.Orange, "Beat the Orks away from our trading hub!"); break;
                case PoiState.Released: UpdateGps(gps, "Trading Hub [Merchant]", marker.Position, Color.Green, "Our trading hub has been released and in business!"); break;
                case PoiState.Invaded: UpdateGps(gps, "Trading Hub [Ork Mobs]", marker.Position, Color.Orange, "Ork mobs have reclaimed our trading hub... Take it back!"); break;
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

        [ProtoContract]
        sealed class ResponsePayload
        {
            [ProtoMember(1)]
            public List<Marker> Markers;
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

            // ReSharper disable once UnusedMember.Local
            Marker()
            {
            }

            public Marker(string id, Vector3D position, PoiState state)
            {
                Id = id;
                Position = position;
                State = state;
            }
        }
    }
}