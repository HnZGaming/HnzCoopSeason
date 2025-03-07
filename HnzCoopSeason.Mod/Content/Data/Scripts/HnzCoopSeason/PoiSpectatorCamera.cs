using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public static class PoiSpectatorCamera
    {
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.PoiSpectatorCamera".GetHashCode();

        public static void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void RequestPosition(string poiId)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(Payload.Request(poiId));
            if (MyAPIGateway.Session.IsServer) // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
            }
            else // dedi
            {
                MyAPIGateway.Multiplayer.SendMessageToServer(ModKey, bytes, true);
                MyLog.Default.Error("[HnzCoopSeason] PoiSpectatorCamera request sent");
            }
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            if (payload.Type == 1) // request
            {
                MyLog.Default.Error("[HnzCoopSeason] PoiSpectatorCamera request received");

                Vector3D position;
                if (!Session.Instance.TryGetPoiPosition(payload.PoiId, out position))
                {
                    position = Vector3D.Zero;
                }

                bytes = MyAPIGateway.Utilities.SerializeToBinary(Payload.Response(position));
                if (MyAPIGateway.Session.IsServer && !MyAPIGateway.Utilities.IsDedicated) // single player
                {
                    OnMessageReceived(ModKey, bytes, 0, false);
                }
                else // dedi
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, senderId, true);
                    MyLog.Default.Error("[HnzCoopSeason] PoiSpectatorCamera response sent");
                }
            }
            else // response
            {
                MyLog.Default.Error("[HnzCoopSeason] PoiSpectatorCamera response received");
                MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.Spectator, position: payload.Position);
            }
        }


        [ProtoContract]
        sealed class Payload
        {
            [ProtoMember(1)]
            public byte Type;

            [ProtoMember(2)]
            public string PoiId;

            [ProtoMember(3)]
            public Vector3D Position;

            // ReSharper disable once UnusedMember.Local
            public Payload()
            {
            }

            public static Payload Request(string poiId) => new Payload
            {
                Type = 1,
                PoiId = poiId,
            };

            public static Payload Response(Vector3D position) => new Payload()
            {
                Type = 2,
                Position = position,
            };
        }
    }
}