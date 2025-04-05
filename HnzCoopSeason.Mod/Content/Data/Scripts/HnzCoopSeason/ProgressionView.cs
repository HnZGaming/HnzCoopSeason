using System;
using System.Text;
using HnzCoopSeason.Utils.ScreenSpace;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason
{
    public sealed class ProgressionView
    {
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.ProgressionView".GetHashCode();
        public static readonly ProgressionView Instance = new ProgressionView();

        ElementGroup _group;
        Element _peaceMeter;
        Element _levelText;
        Element _minPoiPlayerCountText;

        ProgressionView()
        {
        }

        public void Load()
        {
            MyLog.Default.Debug("[HnzCoopSeason] ProgressionView.Load()");

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _group = new ElementGroup
                {
                    Padding = -0.02,
                    Offset = -0.1,
                };

                StackView.Instance.AddGroup(_group);

                _peaceMeter = new Element().AddedTo(_group);
                _levelText = new Element().AddedTo(_group);
                _minPoiPlayerCountText = new Element().AddedTo(_group);
            }
        }

        public void Unload()
        {
            MyLog.Default.Debug("[HnzCoopSeason] ProgressionView.Unload()");

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _group.Clear();
                StackView.Instance.RemoveGroup(_group);
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
                UpdateTexts(payload);
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

        void UpdateTexts(Payload payload) // client
        {
            _peaceMeter.SetText(CreateProgressionHudText(payload.Progress).ToString());
            _levelText.SetText($"Orks Level: {payload.ProgressionLevel}");
            _minPoiPlayerCountText.SetText($"You need {payload.MinPoiPlayerCount} players to challenge Orks.");
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
            // ReSharper disable once MemberCanBePrivate.Local
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
    }
}