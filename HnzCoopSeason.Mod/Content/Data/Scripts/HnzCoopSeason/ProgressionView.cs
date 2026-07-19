using HnzCoopSeason.HudUtils;
using HnzUtils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason
{
    public sealed class ProgressionView
    {
        const string SubtitleUnderOrks = "Orks have taken over Merchants' trading hubs... Send help!";
        const string SubtitleLiberated = "Every Merchants' trading hub is free... The sector is at peace!";

        static readonly ushort ModKey = VRageUtils.StableKey("HnzCoopSeason.ProgressionView");
        public static readonly ProgressionView Instance = new ProgressionView();

        MeterState _state;

        public void Load()
        {
            MyLog.Default.Debug("[HnzCoopSeason] ProgressionView.Load()");

            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                _state = new MeterState
                {
                    BarName = "PEACEMETER",
                    Subtitle = SubtitleUnderOrks, // replaced per-payload once progress arrives
                };

                ScreenTopHud.Instance.AddGroup(nameof(ProgressionView), _state, 0);
            }
        }

        public void Unload()
        {
            MyLog.Default.Debug("[HnzCoopSeason] ProgressionView.Unload()");

            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);

            // client
            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                ScreenTopHud.Instance.RemoveGroup(nameof(ProgressionView));
                _state = null;
            }
        }

        public void UpdateClient() // called in client, every frame
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 15 != 0) return;
            if (_state == null) return;

            // yield the screen-top slot while WeaponCore's target HUD is showing (opt-in)
            ScreenTopHud.Instance.SetActive(nameof(ProgressionView), !CoopHud.YieldToWeaponCore);
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

                MyLog.Default.Info($"[HnzCoopSeason] payload sent: {payload}");
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
            MyLog.Default.Info($"[HnzCoopSeason] UpdateTexts({payload})");
            if (_state == null) return;

            var p100 = payload.Progress * 100;
            _state.Progress = payload.Progress;
            _state.ValueText = p100 == 0 ? "0%" : p100 < 1f ? $"{p100:0.0}%" : $"{p100:0}%";
            _state.Title = $"Sector Liberation - Tier {payload.ProgressionLevel}";

            // same >= 1f test the bar uses to turn green, so the two flip together
            _state.Subtitle = payload.Progress >= 1f ? SubtitleLiberated : SubtitleUnderOrks;

            _state.Description = payload.MinPoiPlayerCount > 1
                ? $"You need {payload.MinPoiPlayerCount} players to challenge Orks."
                : "";
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

            public override string ToString()
            {
                return $"{nameof(Type)}: {Type}, {nameof(Progress)}: {Progress}, {nameof(MinPoiPlayerCount)}: {MinPoiPlayerCount}, {nameof(ProgressionLevel)}: {ProgressionLevel}";
            }
        }
    }
}
