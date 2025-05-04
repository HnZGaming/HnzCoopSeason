using System;
using System.Collections.Generic;
using HnzCoopSeason.Utils.Pools;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace HnzCoopSeason.Utils
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
                if (!CanReach(p, gps.Position, gps.Radius)) continue;

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

                if (!entry.Mute)
                {
                    VRageUtils.PlaySound("HudGPSNotification3");
                }

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
            if (!VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer)) return;

            foreach (var kvp in _wraps)
            {
                UpdateEntityMapping(kvp.Value);
                UpdatePosition(kvp.Value);
            }

            RemoveExpiredGps();
        }

        static void UpdateEntityMapping(Wrap g)
        {
            var targetId = g.Entry.EntityId;
            if (targetId == 0)
            {
                g.Entity = null;
                return;
            }

            if (g.Entity?.EntityId == targetId) return;

            g.Entity = MyAPIGateway.Entities.GetEntityById(targetId);
            //MyLog.Default.WriteLine($"[HnzCoopSeason] mapping entity to gps; name: '{g.Entry.Name}', entity: '{g.Entity}' ({g.Entry.EntityId})");
        }

        static void UpdatePosition(Wrap g)
        {
            g.Gps.Coords = g.Entity != null
                ? g.Entity.GetPosition()
                : g.Entry.Position;
        }

        void RemoveExpiredGps()
        {
            var expiredIds = ListPool<long>.Instance.Get();
            foreach (var kvp in _wraps)
            {
                var wrap = kvp.Value;
                if (ShouldRemove(wrap))
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

        static bool ShouldRemove(Wrap wrap)
        {
            if ((DateTime.UtcNow - wrap.LastUpdate).TotalSeconds > wrap.Entry.Duration) return true;
            if (!CanReach(MyAPIGateway.Session.Player, wrap.Entry.Position, wrap.Entry.Radius)) return true;

            return false;
        }

        static bool CanReach(IMyPlayer player, Vector3D origin, double radius)
        {
            if (radius <= 0) return true; // everyone

            var character = player.Character;
            if (character == null) return false;

            var sphere = new BoundingSphereD(origin, radius);
            if (sphere.Contains(character.GetPosition()) == ContainmentType.Disjoint) return false;

            return true;
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

            [ProtoMember(6)]
            public double Radius { get; set; }

            [ProtoMember(7)]
            public long EntityId { get; set; }

            [ProtoMember(8)]
            public bool Mute { get; set; }
        }

        sealed class Wrap
        {
            public readonly IMyGps Gps;
            public Entry Entry;
            public DateTime LastUpdate;
            public IMyEntity Entity;

            public Wrap(IMyGps gps, Entry entry, DateTime lastUpdate)
            {
                Entry = entry;
                Gps = gps;
                LastUpdate = lastUpdate;
            }
        }
    }
}