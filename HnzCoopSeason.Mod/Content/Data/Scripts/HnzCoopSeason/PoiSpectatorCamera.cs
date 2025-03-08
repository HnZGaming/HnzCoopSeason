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

        public static void SendPosition(string poiId, ulong steamId)
        {
            Vector3D position;
            if (!Session.Instance.TryGetPoiPosition(poiId, out position))
            {
                position = Vector3D.Zero;
            }

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(position));
            if (MyAPIGateway.Utilities.IsDedicated) // dedi
            {
                MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, steamId, true);
                MyLog.Default.Info("[HnzCoopSeason] PoiSpectatorCamera position sent");
            }
            else // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
            }
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;

            MyLog.Default.Info("[HnzCoopSeason] PoiSpectatorCamera position received");
            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            MyAPIGateway.Session.SetCameraController(MyCameraControllerEnum.Spectator, position: payload.Position);
        }

        [ProtoContract]
        sealed class Payload
        {
            [ProtoMember(1)]
            public Vector3D Position;

            // ReSharper disable once UnusedMember.Local
            public Payload()
            {
            }

            public Payload(Vector3D position)
            {
                Position = position;
            }
        }
    }
}