using System;
using System.Collections.Generic;
using HnzUtils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.POI
{
    public class PoiMapDebugView
    {
        public static readonly PoiMapDebugView Instance = new PoiMapDebugView();
        readonly NetworkMessenger _messenger;
        readonly HashSet<IMyGps> _localGpsSet;

        PoiMapDebugView()
        {
            _messenger = new NetworkMessenger("HnzCoopSeason.PoiMapDebugView");
            _localGpsSet = new HashSet<IMyGps>();
        }

        public void Load()
        {
            _messenger.Load(OnMessageReceived);
        }

        public void Unload()
        {
            _messenger.Unload();
            _localGpsSet.Clear();
        }

        public void RemoveAll(ulong steamId)
        {
            ReplaceAll(steamId, Array.Empty<IPoi>());
        }

        public void ReplaceAll(ulong steamId, IEnumerable<IPoi> pois)
        {
            var gpsList = new List<Gps>();
            foreach (var p in pois)
            {
                gpsList.Add(new Gps($"{p.Id}:{p.State}", p.Position));
            }

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(gpsList));
            _messenger.SendTo(steamId, bytes);
        }

        void OnMessageReceived(ulong senderId, byte[] bytes)
        {
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