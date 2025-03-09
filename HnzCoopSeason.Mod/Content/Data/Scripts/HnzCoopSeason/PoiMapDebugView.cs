using System;
using System.Collections.Generic;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason
{
    public static class PoiMapDebugView
    {
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.PoiMapDebugView".GetHashCode();
        static readonly HashSet<IMyGps> _localGpsSet;

        static PoiMapDebugView()
        {
            _localGpsSet = new HashSet<IMyGps>();
        }

        public static void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void Unload()
        {
            _localGpsSet.Clear();
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void RemoveAll(ulong steamId)
        {
            ReplaceAll(steamId, Array.Empty<Poi>());
        }

        public static void ReplaceAll(ulong steamId, IEnumerable<IPoi> pois)
        {
            var gpsList = new List<Gps>();
            foreach (var p in pois)
            {
                gpsList.Add(new Gps(p.Id, p.Position));
            }

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(gpsList));
            MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, steamId, true);
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;

            foreach (var g in _localGpsSet)
            {
                MyAPIGateway.Session.GPS.RemoveLocalGps(g);
            }

            _localGpsSet.Clear();

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            if (payload.GpsList == null) return;

            foreach (var g in payload.GpsList)
            {
                var gps = MyAPIGateway.Session.GPS.Create(g.Name, "", g.Position, true);
                MyAPIGateway.Session.GPS.AddLocalGps(gps);
                _localGpsSet.Add(gps);
            }
        }

        [ProtoContract]
        sealed class Payload
        {
            [ProtoMember(1)]
            public List<Gps> GpsList;

            // ReSharper disable once UnusedMember.Local
            public Payload()
            {
            }

            public Payload(List<Gps> gpsList)
            {
                GpsList = gpsList;
            }
        }

        [ProtoContract]
        sealed class Gps
        {
            [ProtoMember(1)]
            public string Name;

            [ProtoMember(2)]
            public Vector3D Position;

            // ReSharper disable once UnusedMember.Local
            public Gps()
            {
            }

            public Gps(string name, Vector3D position)
            {
                Name = name;
                Position = position;
            }
        }
    }
}