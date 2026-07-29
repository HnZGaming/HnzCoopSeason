using System;
using System.Collections.Generic;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.POI
{
    /// <summary>
    /// client-side store of hidden POI markers (SP + DS client); the DS never loads it.
    /// </summary>
    public sealed class GpsVisibilityStore
    {
        const string FileNamePrefix = "HnzCoopSeason.GpsVisibility";

        readonly HashSet<string> _hidden = new HashSet<string>();
        string _fileName;

        #region Query

        public bool IsVisible(string markerId) => !_hidden.Contains(markerId);

        #endregion

        #region From Player

        public void CaptureChanges(IEnumerable<KeyValuePair<string, IMyGps>> markers)
        {
            var changed = false;
            foreach (var marker in markers)
            {
                changed |= CaptureChange(marker.Key, marker.Value);
            }
            if (changed) Save();
        }

        // returns true if the player toggled this marker since the last pass
        bool CaptureChange(string markerId, IMyGps gps)
        {
            var isHiddenNow = !gps.ShowOnHud;
            var wasHidden = _hidden.Contains(markerId);
            if (isHiddenNow == wasHidden) return false;

            //edge detected
            if (isHiddenNow) _hidden.Add(markerId);
            else _hidden.Remove(markerId);

            return true;
        }

        #endregion

        #region From Server

        // server-forced visibility (e.g. boss entering/leaving range, or the poi's situation changing)
        public void SetHidden(string markerId, bool hidden)
        {
            var changed = hidden ? _hidden.Add(markerId) : _hidden.Remove(markerId);
            if (changed) Save();
        }

        #endregion

        #region Cleanup

        /// <summary>Un-hides markers absent from the server payload — their dismissed situation is over. Saves if changed.</summary>
        /// <param name="presentIds">Marker ids the server still reports; hidden ones not in here are un-hidden.</param>
        public void PruneAbsent(ICollection<string> presentIds)
        {
            if (_hidden.Count == 0) return;

            List<string> gone = null;
            foreach (var id in _hidden)
            {
                if (presentIds.Contains(id)) continue;

                if (gone == null) gone = new List<string>();
                gone.Add(id);
            }

            if (gone == null) return;

            foreach (var id in gone)
            {
                _hidden.Remove(id);
                MyLog.Default.Info($"[HnzCoopSeason] gps {id} un-hidden: no longer reported by server");
            }

            Save();
        }

        #endregion

        #region Storage

        /// <summary>Local storage is shared by every world, and poi ids repeat, so the file is per world.</summary>
        static string GetFileName()
        {
            var multiplayer = MyAPIGateway.Multiplayer;
            var scope = multiplayer != null && !multiplayer.IsServer
                ? "server-" + multiplayer.ServerId
                : "local-" + MyAPIGateway.Session.Name;

            return $"{FileNamePrefix}.{VRageUtils.StableKey(scope):X4}.xml";
        }

        public void Load()
        {
            _hidden.Clear();
            _fileName = GetFileName();

            try
            {
                if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(_fileName, typeof(GpsVisibilityStore))) return;

                using (var reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(_fileName, typeof(GpsVisibilityStore)))
                {
                    var payload = MyAPIGateway.Utilities.SerializeFromXML<Payload>(reader.ReadToEnd());
                    if (payload?.HiddenMarkers == null) return;

                    foreach (var id in payload.HiddenMarkers)
                    {
                        if (id != null) _hidden.Add(id);
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed loading gps visibility; all markers will show; {e}");
            }
        }

        void Save()
        {
            if (_fileName == null) return;

            try
            {
                var payload = new Payload();
                foreach (var id in _hidden)
                {
                    payload.HiddenMarkers.Add(id);
                }

                using (var writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(_fileName, typeof(GpsVisibilityStore)))
                {
                    writer.Write(MyAPIGateway.Utilities.SerializeToXML(payload));
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] failed saving gps visibility; {e}");
            }
        }

        #endregion

        #region Serialization

        public sealed class Payload
        {
            public List<string> HiddenMarkers = new List<string>();
        }

        #endregion
    }
}
