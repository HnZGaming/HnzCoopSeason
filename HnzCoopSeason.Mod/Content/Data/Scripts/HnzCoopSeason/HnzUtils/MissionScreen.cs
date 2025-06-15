using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzUtils
{
    public static class MissionScreen
    {
        static ushort _modKey;

        public static void Load(ushort modKey)
        {
            _modKey = modKey;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(modKey, OnMessageReceived);
        }

        public static void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(_modKey, OnMessageReceived);
        }

        public static void Send(ulong steamId, string title, string message, bool clipboard)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(title, message, clipboard));
            MyAPIGateway.Multiplayer.SendMessageTo(_modKey, bytes, steamId, true);
            MyLog.Default.Debug("[HnzUtils] screen message sent");
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != _modKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            MyAPIGateway.Utilities.ShowMissionScreen(
                "COOP",
                currentObjective: payload.Title,
                screenDescription: payload.Message,
                currentObjectivePrefix: "",
                okButtonCaption: "Copy to clipboard",
                callback: r => Callback(payload, r));
            MyLog.Default.Debug("[HnzUtils] screen message received");
        }

        static void Callback(Payload payload, ResultEnum result)
        {
            if (!payload.Clipboard) return;
            if (result != ResultEnum.OK) return;

            MyClipboardHelper.SetClipboard(payload.Message);
            MyLog.Default.Info("[HnzUtils] set message to clipboard");
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