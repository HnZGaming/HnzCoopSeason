using ProtoBuf;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public static class Communication
    {
        static readonly ushort ModKey = (ushort)"HnzPveSeason.Communication".GetHashCode();

        public static void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void SendMessage(ulong steamId, Color color, string message)
        {
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            MyVisualScriptLogicProvider.SendChatMessageColored(message, color, "pve", playerId);
        }

        public static void ShowScreenMessage(ulong steamId, string title, string message)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(title, message));
            MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, steamId, true);
            MyLog.Default.Info("[HnzPveSeason] screen message sent");
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            MyAPIGateway.Utilities.ShowMissionScreen("pve", currentObjective: payload.Title, screenDescription: payload.Message);
            MyLog.Default.Info("[HnzPveSeason] screen message received");
        }

        [ProtoContract]
        sealed class Payload
        {
            [ProtoMember(1)]
            public string Title;

            [ProtoMember(2)]
            public string Message;

            // ReSharper disable once UnusedMember.Local
            public Payload()
            {
            }

            public Payload(string title, string message)
            {
                Title = title;
                Message = message;
            }
        }
    }
}