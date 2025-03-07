using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason
{
    public static class MissionScreenView
    {
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.MissionScreenView".GetHashCode();

        public static void Load()
        {
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public static void ShowScreenMessage(ulong steamId, string title, string message, bool clipboard)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(title, message, clipboard));
            MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, steamId, true);
            MyLog.Default.Debug("[HnzCoopSeason] screen message sent");
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            MyAPIGateway.Utilities.ShowMissionScreen(
                "COOP",
                currentObjective: payload.Title,
                screenDescription: payload.Message,
                currentObjectivePrefix: "",
                okButtonCaption: "Copy to clipboard",
                callback: r => Callback(payload, r));
            MyLog.Default.Debug("[HnzCoopSeason] screen message received");
        }

        static void Callback(Payload payload, ResultEnum result)
        {
            if (!payload.Clipboard) return;
            if (result != ResultEnum.OK) return;

            MyClipboardHelper.SetClipboard(payload.Message);
            MyLog.Default.Info("[HnzCoopSeason] set message to clipboard");
        }

        [ProtoContract]
        sealed class Payload
        {
            [ProtoMember(1)]
            public string Title;

            [ProtoMember(2)]
            public string Message;

            [ProtoMember(3)]
            public bool Clipboard;

            // ReSharper disable once UnusedMember.Local
            public Payload()
            {
            }

            public Payload(string title, string message, bool clipboard)
            {
                Title = title;
                Message = message;
                Clipboard = clipboard;
            }
        }
    }
}