using Sandbox.ModAPI;

namespace HnzUtils
{
    public sealed class NetworkMessenger
    {
        public delegate void OnMessageReceived(ulong senderId, byte[] bytes);

        readonly ushort _key;
        readonly OnMessageReceived _handler;

        public NetworkMessenger(object key, OnMessageReceived handler)
        {
            _key = (ushort)key.GetHashCode();
            _handler = handler;
        }

        public void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_key, HandleMessage);
        }

        public void Unload()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(_key, HandleMessage);
        }

        public void SendToServer(byte[] bytes)
        {
            if (VRageUtils.NetworkType == NetworkType.SinglePlayer)
            {
                _handler(MyAPIGateway.Session.LocalHumanPlayer.SteamUserId, bytes);
                return;
            }

            MyAPIGateway.Multiplayer.SendMessageToServer(_key, bytes);
        }

        public void SendToOthers(byte[] bytes)
        {
            if (VRageUtils.NetworkType == NetworkType.SinglePlayer)
            {
                _handler(MyAPIGateway.Session.LocalHumanPlayer.SteamUserId, bytes);
                return;
            }

            MyAPIGateway.Multiplayer.SendMessageToOthers(_key, bytes);
        }

        public void SendTo(ulong steamId, byte[] bytes)
        {
            var mySteamId = MyAPIGateway.Session.LocalHumanPlayer?.SteamUserId;
            if (VRageUtils.NetworkType == NetworkType.SinglePlayer &&
                mySteamId == steamId)
            {
                _handler(steamId, bytes);
                return;
            }

            MyAPIGateway.Multiplayer.SendMessageTo(_key, bytes, steamId);
        }

        void HandleMessage(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != _key) return;
            _handler(senderId, bytes);
        }
    }
}