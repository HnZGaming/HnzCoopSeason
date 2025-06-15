using HnzUtils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.POI
{
    public class PoiSpectatorCamera
    {
        public static readonly PoiSpectatorCamera Instance = new PoiSpectatorCamera();
        readonly NetworkMessenger _messenger;

        PoiSpectatorCamera()
        {
            _messenger = new NetworkMessenger("HnzCoopSeason.PoiSpectatorCamera");
        }

        public void Load()
        {
            _messenger.Load(OnMessageReceived);
        }

        public void Unload()
        {
            _messenger.Unload();
        }

        public void SendPosition(string poiId, ulong steamId)
        {
            Vector3D position;
            if (!Session.Instance.TryGetPoiPosition(poiId, out position))
            {
                position = Vector3D.Zero;
            }

            MyLog.Default.Info($"[HnzCoopSeason] PoiSpectatorCamera position sent: {position}");
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(position));
            _messenger.SendTo(steamId, bytes);
        }

        void OnMessageReceived(ulong senderId, byte[] bytes)
        {
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