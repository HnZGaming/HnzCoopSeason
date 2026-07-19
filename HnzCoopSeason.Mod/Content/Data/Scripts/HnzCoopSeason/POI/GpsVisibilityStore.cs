using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.POI
{
    // client-side store of hidden markers; unknown ids default to visible
    public sealed class GpsVisibilityStore
    {
        const string FileName = "HnzCoopSeason.GpsVisibility.xml";

        readonly Dictionary<string, PoiState> _hidden = new Dictionary<string, PoiState>();
        readonly Dictionary<string, PoiState> _lastSeenState = new Dictionary<string, PoiState>();

        public bool IsVisible(string markerId) => !_hidden.ContainsKey(markerId);

        // true when the state moved on since the player hid it; call once per server response
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

        // absence from the payload means the dismissed situation is over
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

        // the gps panel mutates our own MyGps instance, so reading ShowOnHud back is enough
        public void CaptureChanges(IEnumerable<KeyValuePair<string, IMyGps>> markers)
        {
            var changed = false;

            foreach (var marker in markers)
            {
                var hidden = !marker.Value.ShowOnHud;
                if (hidden == _hidden.ContainsKey(marker.Key)) continue;

                if (hidden)
                {
                    // stamp the state it was dismissed in
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
