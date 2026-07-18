using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.POI
{
    /// <summary>
    ///     Client-side memory of which POI markers the player hid in the GPS panel.
    ///     The game will never do this for us: local GPS entries are deliberately excluded
    ///     from the world save (MyGpsCollection.SaveGpss skips gps.IsLocal), so every rejoin
    ///     recreates our markers from scratch and they come back shown.
    ///     Only hidden ids are stored — an unknown marker defaults to visible, so brand new
    ///     POIs (and a first-ever join) show up without needing an entry.
    ///     Each hidden id remembers the PoiState it was hidden in: the player dismissed *that*
    ///     situation, so when the POI flips (released -> invaded, occupied -> released, ...)
    ///     the marker un-hides itself and demands attention again.
    /// </summary>
    public sealed class GpsVisibilityStore
    {
        const string FileName = "HnzCoopSeason.GpsVisibility.xml";

        readonly Dictionary<string, PoiState> _hidden = new Dictionary<string, PoiState>();
        readonly Dictionary<string, PoiState> _lastSeenState = new Dictionary<string, PoiState>();

        public bool IsVisible(string markerId) => !_hidden.ContainsKey(markerId);

        /// <summary>
        ///     Records the marker's current state and reports whether that state has moved on
        ///     since the player hid it — in which case the hide is dropped and the caller should
        ///     rebuild the GPS so it shows again. Call once per marker per server response.
        /// </summary>
        public bool ClearIfStateChanged(string markerId, PoiState state)
        {
            _lastSeenState[markerId] = state;

            PoiState hiddenIn;
            if (!_hidden.TryGetValue(markerId, out hiddenIn) || hiddenIn == state) return false;

            _hidden.Remove(markerId);
            Save();
            MyLog.Default.Info($"[HnzCoopSeason] gps {markerId} un-hidden: {hiddenIn} -> {state}");
            return true;
        }

        /// <summary>
        ///     Drops hides for markers the server no longer sends. Absence means the situation the
        ///     player dismissed is over: the server only ships nearby POIs, ALL invaded ones, and a
        ///     single nearest merchant. So a POI hidden while Invaded stays in the payload for as
        ///     long as that invasion lasts, and falls out of it the moment the invasion ends.
        ///     Without this, a hide stamped "Invaded" survives release-and-reinvade — the state
        ///     value returns to Invaded, the comparison sees no change, and the marker never
        ///     comes back. Call once per server response, after processing the markers.
        /// </summary>
        public void PruneAbsent(ICollection<string> presentIds)
        {
            if (_hidden.Count == 0) return;

            List<string> gone = null;
            foreach (var id in _hidden.Keys)
            {
                if (presentIds.Contains(id)) continue;

                if (gone == null) gone = new List<string>();
                gone.Add(id);
            }

            if (gone == null) return;

            foreach (var id in gone)
            {
                _hidden.Remove(id);
                _lastSeenState.Remove(id);
                MyLog.Default.Info($"[HnzCoopSeason] gps {id} un-hidden: no longer reported by server");
            }

            Save();
        }

        public void Load()
        {
            _hidden.Clear();
            _lastSeenState.Clear();

            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(FileName, typeof(GpsVisibilityStore))) return;

                using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(FileName, typeof(GpsVisibilityStore)))
                {
                    var payload = MyAPIGateway.Utilities.SerializeFromXML<Payload>(reader.ReadToEnd());
                    if (payload?.HiddenMarkers == null) return;

                    foreach (var entry in payload.HiddenMarkers)
                    {
                        if (entry?.MarkerId == null) continue;

                        _hidden[entry.MarkerId] = entry.State;
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed loading gps visibility; all markers will show; {e}");
            }
        }

        /// <summary>
        ///     Picks up toggles the player made in the GPS panel. The panel mutates the very
        ///     MyGps instance we handed to AddLocalGps (MyGpsCollection.ShowOnHudSuccess),
        ///     so reading ShowOnHud back off our own reference is enough — no events needed.
        ///     Writes to disk only when something actually changed.
        /// </summary>
        public void CaptureChanges(IEnumerable<KeyValuePair<string, IMyGps>> markers)
        {
            var changed = false;

            foreach (var marker in markers)
            {
                var hidden = !marker.Value.ShowOnHud;
                if (hidden == _hidden.ContainsKey(marker.Key)) continue;

                if (hidden)
                {
                    // stamp the state it was dismissed in, so a later flip can revive it
                    PoiState state;
                    _hidden[marker.Key] = _lastSeenState.TryGetValue(marker.Key, out state) ? state : PoiState.Occupied;
                }
                else
                {
                    _hidden.Remove(marker.Key);
                }

                changed = true;
            }

            if (changed) Save();
        }

        void Save()
        {
            try
            {
                var payload = new Payload();
                foreach (var pair in _hidden)
                {
                    payload.HiddenMarkers.Add(new Entry { MarkerId = pair.Key, State = pair.Value });
                }

                using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FileName, typeof(GpsVisibilityStore)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(payload));
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed saving gps visibility; {e}");
            }
        }

        public sealed class Payload
        {
            public List<Entry> HiddenMarkers = new List<Entry>();
        }

        public sealed class Entry
        {
            public string MarkerId;
            public PoiState State;
        }
    }
}
