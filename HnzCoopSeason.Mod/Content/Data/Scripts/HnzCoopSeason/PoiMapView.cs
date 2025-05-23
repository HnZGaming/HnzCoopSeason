﻿using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class PoiMapView
    {
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.PoiMapView".GetHashCode();
        public static readonly PoiMapView Instance = new PoiMapView();
        readonly LocalGpsCollection<string> _markers;

        PoiMapView()
        {
            _markers = new LocalGpsCollection<string>();
        }

        public void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);
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
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(Payload.Request());

            if (MyAPIGateway.Session.IsServer) // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
            }
            else // dedi client
            {
                MyLog.Default.Debug("[HnzCoopSeason] PoiMapView sending request");
                MyAPIGateway.Multiplayer.SendMessageToServer(ModKey, bytes, true);
            }
        }

        void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;
            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);

            if (payload.Type == 1) // request
            {
                SendMarkersToClient(senderId);
            }
            else // response
            {
                DeployMap(payload.Markers);
            }
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

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(Payload.Response(markers));

            if (MyAPIGateway.Utilities.IsDedicated) // dedi
            {
                MyLog.Default.Debug("[HnzCoopSeason] PoiMapView sending response");
                MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, steamId, true);
            }
            else // single player
            {
                // ReSharper disable once TailRecursiveCall
                OnMessageReceived(ModKey, bytes, 0, false);
            }
        }

        static IEnumerable<IPoi> GetPois(Vector3D origin)
        {
            var foundMerchant = false;
            var foundPending = false;
            foreach (var poi in Session.Instance.GetAllPois().OrderBy(p => Vector3D.Distance(p.Position, origin)))
            {
                // ReSharper disable once ConvertIfStatementToSwitchStatement
                if (poi.State == PoiState.Released)
                {
                    if (foundMerchant) continue;
                    foundMerchant = true;
                }
                else if (poi.State == PoiState.Pending)
                {
                    if (foundPending) continue;
                    foundPending = true;
                }

                yield return poi;
            }
        }

        void DeployMap(List<Marker> markers) // client
        {
            // remove old markers
            _markers.RemoveExceptFor(markers.Select(m => m.Id));

            // add new markers
            foreach (var marker in markers)
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

        static void UpdateGps(IMyGps gps, Marker marker)
        {
            switch (marker.State)
            {
                case PoiState.Occupied: UpdateGps(gps, "Trading Hub [Orks]", marker.Position, Color.Orange, "Beat the Orks away from our trading hub!"); break;
                case PoiState.Released: UpdateGps(gps, "Trading Hub [Merchant]", marker.Position, Color.Green, "Our trading hub has been released and in business!"); break;
                case PoiState.Pending: UpdateGps(gps, "Trading Hub [Pending]", marker.Position, Color.White, "All planetary POIs must be released first!"); break;
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
        sealed class Payload
        {
            [ProtoMember(1)]
            public byte Type;

            [ProtoMember(2)]
            public List<Marker> Markers;

            // ReSharper disable once UnusedMember.Local
            Payload()
            {
            }

            public static Payload Request() => new Payload
            {
                Type = 1,
            };

            public static Payload Response(List<Marker> markers) => new Payload
            {
                Type = 2,
                Markers = markers,
            };
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