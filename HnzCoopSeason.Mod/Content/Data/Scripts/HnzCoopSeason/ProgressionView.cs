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
    public sealed class ProgressionView
    {
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.ProgressionView".GetHashCode();

        HudAPIv2 _hudApi;
        TextLineView _peaceMeter;
        TextLineView _levelText;
        TextLineView _minPoiPlayerCountText;

        public void Load()
        {
            MyLog.Default.Debug("[HnzCoopSeason] ProgressionView.Load()");

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _hudApi = new HudAPIv2();
                _peaceMeter = new TextLineView();
                _levelText = new TextLineView();
                _minPoiPlayerCountText = new TextLineView();
            }
        }

        public void Unload()
        {
            MyLog.Default.Debug("[HnzCoopSeason] ProgressionView.Unload()");

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _hudApi.Close();
                _hudApi.Unload();
                _hudApi = null;
                _peaceMeter = null;
                _levelText = null;
                _minPoiPlayerCountText = null;
            }
        }

        public void RequestUpdate() // called in client
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

        public void UpdateProgress() //called in server
        {
            var payload = CreateUpdatePayload();
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(payload);

            if (MyAPIGateway.Utilities.IsDedicated) // dedi
            {
                MyAPIGateway.Multiplayer.SendMessageToOthers(ModKey, bytes, true);
                MyLog.Default.Info("[HnzCoopSeason] progress sent: {0:0.00}", payload.Progress);
            }
            else // single player
            {
                OnMessageReceived(ModKey, bytes, 0, false);
            }
        }

        void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<Payload>(bytes);
            if (payload.Type == 1) // query
            {
                payload = CreateUpdatePayload();
                bytes = MyAPIGateway.Utilities.SerializeToBinary(payload);

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
                Render(payload);
                MyLog.Default.Info("[HnzCoopSeason] progress received: {0:0.00}", payload.Progress);
            }
        }

        static Payload CreateUpdatePayload()
        {
            var level = Session.Instance.GetProgressLevel();
            var progress = Session.Instance.GetProgress();
            var minPoiPlayerCount = SessionConfig.Instance.ProgressionLevels[level].MinPlayerCount;
            return Payload.Update(progress, minPoiPlayerCount, level);
        }

        void Render(Payload payload) // client
        {
            _peaceMeter.Update(CreateProgressionHudText(payload.Progress), -0.1);
            _levelText.Update(new StringBuilder($"Orks Level: {payload.ProgressionLevel}"), -0.15f);
            _minPoiPlayerCountText.Update(new StringBuilder($"You need {payload.MinPoiPlayerCount} players to challenge Orks."), -0.2f);
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

            [ProtoMember(3)]
            public int MinPoiPlayerCount;

            [ProtoMember(4)]
            public int ProgressionLevel;

            // ReSharper disable once EmptyConstructor
            public Payload()
            {
            }

            public static Payload Request() => new Payload
            {
                Type = 1
            };

            public static Payload Update(float progress, int minPoiPlayerCount, int progressionLevel) => new Payload
            {
                Type = 2,
                Progress = progress,
                MinPoiPlayerCount = minPoiPlayerCount,
                ProgressionLevel = progressionLevel
            };
        }

        sealed class TextLineView
        {
            HudAPIv2.HUDMessage _message;

            public void Update(StringBuilder text, double y)
            {
                _message?.DeleteMessage();
                _message = new HudAPIv2.HUDMessage(
                    /*text*/ text,
                    /*origin*/ new Vector2D(0f, 1f),
                    /*offset*/ new Vector2D(0f, y),
                    /*time to live*/ -1,
                    /*scale*/ 1,
                    /*hide hud*/ true,
                    /*shadowing*/ false,
                    /*shadow color*/ null,
                    /*text*/ MyBillboard.BlendTypeEnum.PostPP);

                // center align in X axis
                var textLength = _message.GetTextLength().X;
                _message.Offset = new Vector2D(-textLength / 2, _message.Offset.Y);
            }
        }
    }
}