using System;
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

        public static void RequestUpdate() // called in client
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(Payload.Request());

            if (MyAPIGateway.Session.IsServer) // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
            }
            else // dedi
            {
                MyAPIGateway.Multiplayer.SendMessageToServer(ModKey, bytes, true);
            }
        }

        public static void UpdateProgress() //called in server
        {
            var progress = Session.Instance.GetProgress();
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(Payload.Update(progress));

            if (MyAPIGateway.Utilities.IsDedicated) // dedi
            {
                MyAPIGateway.Multiplayer.SendMessageToOthers(ModKey, bytes, true);
                MyLog.Default.Info("[HnzCoopSeason] progress sent: {0:0.00}", progress);
            }
            else // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
            }
        }

        static void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            if (payload.Type == 1) // query
            {
                var progress = Session.Instance.GetProgress();
                bytes = MyAPIGateway.Utilities.SerializeToBinary(Payload.Update(progress));

                if (MyAPIGateway.Utilities.IsDedicated) // dedi
                {
                    MyAPIGateway.Multiplayer.SendMessageTo(ModKey, bytes, senderId, true);
                }
                else // single player
                {
                    OnMessageReceived(ModKey, bytes, 0, false);
                }
            }
            else // update
            {
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
            [ProtoMember(1)]
            public byte Type;

            [ProtoMember(2)]
            public float Progress;

            // ReSharper disable once EmptyConstructor
            public Payload()
            {
            }

            public static Payload Request() => new Payload
            {
                Type = 1
            };

            public static Payload Update(float progress) => new Payload
            {
                Type = 2,
                Progress = progress,
            };
        }
    }
}