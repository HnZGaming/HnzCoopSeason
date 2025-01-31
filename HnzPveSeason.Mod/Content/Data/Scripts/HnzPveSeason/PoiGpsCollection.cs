using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Utils;

namespace HnzPveSeason
{
    //todo local gps
    public sealed class PoiGpsCollection
    {
        const string VariableKey = "HnzPveSeason.PoiGpsCollection";
        readonly Dictionary<ulong, List<int>> _gpsHashes;

        public PoiGpsCollection()
        {
            _gpsHashes = new Dictionary<ulong, List<int>>();
        }

        public void Load()
        {
            string dataStr;
            MyAPIGateway.Utilities.GetVariable(VariableKey, out dataStr);
            if (string.IsNullOrEmpty(dataStr)) return;

            MyLog.Default.Info($"[HnzPveSeason] poi gps data: '{dataStr}'");

            _gpsHashes.Clear();
            foreach (var kvp in Deserialize<Dictionary<ulong, List<int>>>(dataStr))
            {
                _gpsHashes[kvp.Key] = kvp.Value;
            }
        }

        public void Unload()
        {
            _gpsHashes.Clear();
        }

        public void RemoveAll(ulong steamId)
        {
            List<int> gpsHashes;
            if (!_gpsHashes.TryGetValue(steamId, out gpsHashes)) return;

            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            foreach (var gpsHash in gpsHashes)
            {
                MyAPIGateway.Session.GPS.RemoveGps(playerId, gpsHash);
            }

            _gpsHashes.Remove(steamId);
            Save();
        }

        public void AddAll(ulong steamId, IEnumerable<Poi> pois)
        {
            List<int> gpsHashes;
            if (!_gpsHashes.TryGetValue(steamId, out gpsHashes))
            {
                gpsHashes = new List<int>();
                _gpsHashes[steamId] = gpsHashes;
            }

            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            foreach (var poi in pois)
            {
                var gps = MyAPIGateway.Session.GPS.Create(poi.Id, "", poi.Position, true);
                MyAPIGateway.Session.GPS.AddGps(playerId, gps);
                gpsHashes.Add(gps.Hash);
            }

            Save();
        }

        void Save()
        {
            var str = Serialize(_gpsHashes);
            MyAPIGateway.Utilities.SetVariable(VariableKey, str);
        }

        static string Serialize<T>(T data)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(data);
            return Convert.ToBase64String(bytes);
        }

        static T Deserialize<T>(string str)
        {
            var bytes = Convert.FromBase64String(str);
            return MyAPIGateway.Utilities.SerializeFromBinary<T>(bytes);
        }
    }
}