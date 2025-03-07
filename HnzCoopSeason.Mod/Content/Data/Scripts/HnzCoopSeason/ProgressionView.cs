﻿using System;
using System.Text;
using HnzCoopSeason.HudAPI;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace HnzCoopSeason
{
    public static class ProgressionView
    {
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.ProgressionView".GetHashCode();

        static HudAPIv2 _hudApi;
        static HudAPIv2.HUDMessage _hudMessage;

        public static void Load()
        {
            MyLog.Default.Info("[HnzCoopSeason] ProgressionView.Load()");

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _hudApi = new HudAPIv2();
            }
        }

        public static void Unload()
        {
            MyLog.Default.Info("[HnzCoopSeason] ProgressionView.Unload()");

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _hudApi.Close();
                _hudApi.Unload();
                _hudMessage?.DeleteMessage();
            }
        }

        public static void UpdateProgress() //called in server
        {
            var progress = Session.Instance.GetProgress();
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(new Payload(progress));

            if (MyAPIGateway.Session.IsServer && !MyAPIGateway.Utilities.IsDedicated) // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
                return;
            }

            MyAPIGateway.Multiplayer.SendMessageToOthers(ModKey, bytes, true);
            MyLog.Default.Info("[HnzCoopSeason] progress sent: {0:0.00}", progress);
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (MyAPIGateway.Utilities.IsDedicated) return;
            if (modKey != ModKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            var progress = payload.Progress;

            _hudMessage?.DeleteMessage();
            _hudMessage = new HudAPIv2.HUDMessage(
                /*text*/ CreateProgressionHudText(progress),
                /*origin*/ new Vector2D(0f, 1f),
                /*offset*/ new Vector2D(-0.25f, -0.04f),
                /*time to live*/ -1,
                /*scale*/ 1,
                /*hide hud*/ true,
                /*shadowing*/ false,
                /*shadow color*/ null,
                /*text*/ MyBillboard.BlendTypeEnum.PostPP);

            MyLog.Default.Info("[HnzCoopSeason] progress received: {0:0.00}", progress);
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