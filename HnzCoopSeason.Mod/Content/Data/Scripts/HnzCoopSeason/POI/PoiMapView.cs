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
        readonly HashSet<string> _autoHidden = new HashSet<string>(); // hidden by us when a boss marker appeared, not by the player
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

            // catch show/hide toggles the player made in the GPS panel
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 == 0)
            {
                // turning an auto-hidden marker back on countermands the boss suppression:
                // the player wants to see this one, so we stop forcing it down until the
                // boss marker goes away and a later spawn earns a fresh hide
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

        /// <summary>
        ///     Folds show/hide toggles the player made in the GPS panel into the store. Markers we
        ///     auto-hid are excluded: their ShowOnHud is false because WE set it, not the player.
        /// </summary>
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

                // the boss broadcasts its own ORK BOSS marker at the same spot, so drop ours to
                // avoid a "Mixed Signals" cluster -- but only inside the range that marker
                // reaches, or players further out would be left with nothing to navigate to
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

            // flush pending toggles BEFORE the new states land. CaptureChanges otherwise runs on a
            // 60-frame tick and stamps each hide with _lastSeenState, so a marker hidden in the
            // second before a payload arrives gets stamped with the state from THIS payload -- the
            // very transition that was supposed to revive it. hide a merchant, have orks take the
            // hub a moment later, and it records hiddenIn=Invaded against a now-Invaded POI: the
            // comparison sees no change and the marker stays hidden forever. capturing first
            // stamps it against the state the player was actually looking at
            if (_visibilityLoaded) CapturePlayerToggles();

            var presentIds = new HashSet<string>(payload.Markers.Select(m => m.Id));

            foreach (var m in payload.Markers)
            {
                if (m.HideOnHud)
                {
                    // the boss is broadcasting here: hide our marker once, on the transition
                    // into that situation. re-asserting it every response would nail the
                    // marker down and make the GPS panel's show toggle useless
                    if (!_autoHideOverridden.Contains(m.Id) && _autoHidden.Add(m.Id))
                    {
                        MyLog.Default.Info($"[HnzCoopSeason] gps {m.Id} auto-hidden: boss marker in range");
                    }
                }
                else // boss gone or out of its marker's reach; our marker is needed again
                {
                    _autoHidden.Remove(m.Id);
                    _autoHideOverridden.Remove(m.Id);
                }
            }

            PruneAbsent(_autoHidden, presentIds);
            PruneAbsent(_autoHideOverridden, presentIds);

            // remove old markers
            _markers.RemoveExceptFor(presentIds);

            // a hidden marker that dropped out of the server's list had its situation resolved,
            // so the hide expires; otherwise a release-and-reinvade would land on the same
            // PoiState it was hidden in and stay hidden forever
            _visibility.PruneAbsent(presentIds);

            // add new markers
            foreach (var marker in payload.Markers)
            {
                // a POI that changed hands (invaded/occupied/released) revives a hidden marker:
                // dropping the gps here forces the rebuild below, which re-registers the HUD
                // marker — just flipping ShowOnHud would not
                if (_visibility.ClearIfStateChanged(marker.Id, marker.State))
                {
                    _markers.Remove(marker.Id);
                }

                // the player's own choice, unless our one-shot boss auto-hide is still standing
                var showOnHud = _visibility.IsVisible(marker.Id) && !_autoHidden.Contains(marker.Id);

                // same reason as above: assigning ShowOnHud does not re-register the hud marker,
                // so a change in visibility has to go through a rebuild
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
                else // new gps — honour the player's last show/hide choice for this marker
                {
                    gps = MyAPIGateway.Session.GPS.Create("", "", Vector3D.Zero, showOnHud, false);
                    UpdateGps(gps, marker);
                    gps.UpdateHash(); // init hash
                    _markers.Add(marker.Id, gps);
                }
            }
        }

        /// <summary>
        ///     Drops ids the server stopped reporting. Both sets track a live boss situation, so a
        ///     marker leaving the payload ends it — and a later spawn at that POI starts clean.
        /// </summary>
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
            public bool HideOnHud; // boss is broadcasting its own marker here; keep the gps, drop the hud pin

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