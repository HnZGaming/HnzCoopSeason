using System;
using System.Collections.Generic;
using HnzCoopSeason.Utils;
using HnzCoopSeason.Utils.Pools;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class FlashGps
    {
        public static readonly FlashGps Instance = new FlashGps();
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.FlashGPS.FlashGpsSession".GetHashCode();

        Dictionary<long, Wrap> _wraps;

        public void Load()
        {
            if (VRageUtils.NetworkType == NetworkType.DediClient)
            {
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);
            }

            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                _wraps = new Dictionary<long, Wrap>();
            }
        }

        public void Unload()
        {
            if (VRageUtils.NetworkType == NetworkType.DediClient)
            {
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);
            }
        }

        public void Send(Entry gps)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer, "must be a server");

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(gps);
            if (VRageUtils.NetworkType == NetworkType.SinglePlayer)
            {
                OnMessageReceived(ModKey, bytes, 0, false);
                return;
            }

            var players = new List<IMyPlayer>();
            MyAPIGateway.Players.GetPlayers(players);

            foreach (var p in players)
            {
                MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, p.SteamUserId);
            }
        }

        void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer, "must be a client");

            if (modKey != ModKey) return;

            var entry = MyAPIGateway.Utilities.SerializeFromBinary<Entry>(bytes);

            Wrap wrap;
            if (!_wraps.TryGetValue(entry.Id, out wrap))
            {
                var gps = MyAPIGateway.Session.GPS.Create(entry.Name, "", entry.Position, true);
                MyAPIGateway.Session.GPS.AddLocalGps(gps);
                VRageUtils.PlaySound("HudGPSNotification3");
                wrap = new Wrap(gps, entry, DateTime.UtcNow);
                _wraps.Add(entry.Id, wrap);
            }

            wrap.Gps.Name = entry.Name;
            wrap.Gps.Coords = entry.Position;
            wrap.Gps.GPSColor = entry.Color;
            wrap.Entry = entry;
            wrap.LastUpdate = DateTime.UtcNow;
        }

        public void Update()
        {
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                var expiredIds = ListPool<long>.Instance.Get();

                foreach (var kvp in _wraps)
                {
                    var wrap = kvp.Value;
                    var timePast = (DateTime.UtcNow - wrap.LastUpdate).TotalSeconds;
                    if (timePast > wrap.Entry.Duration)
                    {
                        expiredIds.Add(kvp.Key);
                        MyAPIGateway.Session.GPS.RemoveLocalGps(wrap.Gps);
                    }
                }

                foreach (var expiredId in expiredIds)
                {
                    _wraps.Remove(expiredId);
                }

                ListPool<long>.Instance.Release(expiredIds);
            }
        }

        [ProtoContract]
        public sealed class Entry
        {
            [ProtoMember(1)]
            public long Id { get; set; }

            [ProtoMember(2)]
            public string Name { get; set; }

            [ProtoMember(3)]
            public Vector3D Position { get; set; }

            [ProtoMember(4)]
            public Color Color { get; set; }

            [ProtoMember(5)]
            public double Duration { get; set; }
        }

        sealed class Wrap
        {
            public readonly IMyGps Gps;
            public Entry Entry;
            public DateTime LastUpdate;

            public Wrap(IMyGps gps, Entry entry, DateTime lastUpdate)
            {
                Entry = entry;
                Gps = gps;
                LastUpdate = lastUpdate;
            }
        }
    }
}