using System;
using System.Text;
using HnzPveSeason.HudAPI;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace HnzPveSeason
{
    public sealed class ProgressionView
    {
        public static readonly ProgressionView Instance = new ProgressionView();
        static readonly ushort ModKey = (ushort)"HnzPveSeason.ProgressionView".GetHashCode();

        HudAPIv2 _hudApi;
        bool IsApiLoaded => _hudApi?.Heartbeat ?? false;

        public void Load()
        {
            MyLog.Default.Info("[HnzPveSeason] ProgressionView.Load()");

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _hudApi = new HudAPIv2();
            }
        }

        public void Unload()
        {
            MyLog.Default.Info("[HnzPveSeason] ProgressionView.Unload()");

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _hudApi.Close();
                _hudApi.Unload();
            }
        }

        public void UpdateProgress() //called in server
        {
            var progress = Session.Instance.GetProgress();
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(progress));

            if (MyAPIGateway.Session.IsServer && !MyAPIGateway.Utilities.IsDedicated) // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
                return;
            }

            MyAPIGateway.Multiplayer.SendMessageToOthers(ModKey, bytes, true);
            MyLog.Default.Info("[HnzPveSeason] progress sent: {0:0.00}", progress);
        }

        void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;
            if (modKey != ModKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            var progress = payload.Progress;

            // ReSharper disable once ObjectCreationAsStatement
            new HudAPIv2.HUDMessage( // this actually works smh
                /*text*/ CreateProgressionHudText(progress),
                /*origin*/ new Vector2D(0f, 1f),
                /*offset*/ new Vector2D(-0.25f, -0.04f),
                /*time to live*/ -1,
                /*scale*/ 1,
                /*hide hud*/ true,
                /*shadowing*/ false,
                /*shadow color*/ null,
                /*text*/ MyBillboard.BlendTypeEnum.PostPP);

            MyLog.Default.Info("[HnzPveSeason] progress received: {0:0.00}", progress);
        }

        static StringBuilder CreateProgressionHudText(float progress)
        {
            var buffer = new StringBuilder();
            buffer.Append("PEACEMETER ");

            for (var i = 0; i < 100; i++)
            {
                var c = (float)i / 100 < progress ? "0,255,0" : "200,0,0";
                buffer.Append($"<color={c}>|");
            }

            var p100 = progress * 100;
            var pstr = p100 == 0 ? "0" : p100 < 1f ? $"{p100:0.0}" : $"{p100:0}";
            buffer.Append($"<reset> {pstr}%");

            return buffer;
        }

        [ProtoContract]
        sealed class Payload
        {
            // ReSharper disable once UnusedMember.Local
            public Payload()
            {
            }

            public Payload(float progress)
            {
                Progress = progress;
            }

            [ProtoMember(1)]
            public float Progress { get; set; }
        }
    }
}