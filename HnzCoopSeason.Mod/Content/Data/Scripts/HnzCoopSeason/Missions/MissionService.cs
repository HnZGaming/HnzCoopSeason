using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Missions.MissionLogics;
using HnzUtils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Missions
{
    public sealed class MissionService
    {
        public static readonly MissionService Instance = new MissionService();
        NetworkMessenger _syncMessenger;
        NetworkMessenger _queryMessenger;
        NetworkMessenger _submitMessenger;
        List<Mission> _missions;
        IMissionLogic _selectedMissionLogic; // client only

        public IReadOnlyList<Mission> Missions => _missions;
        public int CurrentMissionIndex { get; private set; }

        public Action<Mission[]> OnMissionsReceived; // client only
        public Action<Mission> OnMissionSelected; // client only
        public Action<bool, string> OnClientSubmitEnabledChanged; // client only
        public Action<string> OnClientMissionStatusChanged;

        public void Load()
        {
            _missions = new List<Mission>();

            _syncMessenger = new NetworkMessenger("HnzCoopSeason.Missions.MissionService.Sync");
            _syncMessenger.Load(OnClientReceivedMissions);

            _queryMessenger = new NetworkMessenger("HnzCoopSeason.Missions.MissionService.Query");
            _queryMessenger.Load(OnQueryMessageReceived);

            _submitMessenger = new NetworkMessenger("HnzCoopSeason.Missions.MissionService.Submit");
            _submitMessenger.Load(OnServerReceivedSubmission);
        }

        public void Unload()
        {
            _missions.Clear();
            _syncMessenger.Unload();
            _submitMessenger.Unload();
            _queryMessenger.Unload();
        }

        public void Update()
        {
            if (VRageUtils.NetworkTypeIn(NetworkType.DediClient | NetworkType.SinglePlayer))
            {
                UpdateClient();
            }
        }

        public void UpdateClient()
        {
            _selectedMissionLogic?.OnClientUpdate();
        }

        public void RequestUpdate()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);
            _queryMessenger.SendToServer(Array.Empty<byte>());
        }

        void OnQueryMessageReceived(ulong senderId, byte[] bytes)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            SendMissionsToClient(senderId);
        }

        public void ForceUpdateMission(int index, int progress)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Info($"[HnzCoopSeason] MissionService.ForceUpdateMission({index}, {progress})");

            var config = SessionConfig.Instance.Missions[index];
            var mission = new Mission(config, index);
            CreateLogic(mission, null).ForceProgress(progress);
            UpdateMissions();
        }

        public void UpdateMissions() // server
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Info("[HnzCoopSeason] MissionService.ReadMissions()");

            _missions.Clear();
            CurrentMissionIndex = int.MaxValue;

            var configs = SessionConfig.Instance.Missions ?? Array.Empty<MissionConfig>();
            for (var i = 0; i < configs.Length; i++)
            {
                var c = configs[i];
                MyLog.Default.Info($"[HnzCoopSeason] mission [{i}] config: {c}");

                var mission = new Mission(c, i);
                CreateLogic(mission, null).LoadState();
                MyLog.Default.Info($"[HnzCoopSeason] mission [{i}] progress: {mission.Progress}");

                if (mission.Progress < mission.Goal)
                {
                    CurrentMissionIndex = Math.Min(CurrentMissionIndex, i);
                }

                _missions.Add(mission);
            }

            MyLog.Default.Info($"[HnzCoopSeason] done reading missions; count: {_missions.Count}, current index: {CurrentMissionIndex}");

            var payload = SyncPayload.Create(_missions, CurrentMissionIndex);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(payload);
            _syncMessenger.SendToOthers(bytes);
        }

        void SendMissionsToClient(ulong receiverId)
        {
            var payload = SyncPayload.Create(_missions, CurrentMissionIndex);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(payload);
            _syncMessenger.SendTo(receiverId, bytes);
        }

        void OnClientReceivedMissions(ulong steamId, byte[] bytes)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            MyLog.Default.Info("[HnzCoopSeason] MissionClient.OnSyncClient()");
            var payload = MyAPIGateway.Utilities.SerializeFromBinary<SyncPayload>(bytes);
            _missions.Clear();
            _missions.AddRange(payload.Missions);
            CurrentMissionIndex = payload.CurrentMissionIndex;
            OnMissionsReceived?.Invoke(payload.Missions);

            if (_missions.Count == 0)
            {
                MyLog.Default.Warning("[HnzCoopSeason] no missions received");
                return;
            }

            var missionIndex = _selectedMissionLogic?.Mission.Index ?? payload.CurrentMissionIndex;
            SelectMission(missionIndex);
        }

        public void SelectMission(int missionIndex) // client only
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);

            Mission mission;
            if (!_missions.TryGetElementAt(missionIndex, out mission))
            {
                MyLog.Default.Error($"[HnzCoopSeason] invalid mission index: {missionIndex}; missions count: {_missions.Count}");
                return;
            }

            MyLog.Default.Info($"[HnzCoopSeason] mission [{missionIndex}] selected; progress: {mission.Progress}");

            OnMissionSelected?.Invoke(mission);

            var player = MyAPIGateway.Session.LocalHumanPlayer;
            _selectedMissionLogic = CreateLogic(mission, player);
            _selectedMissionLogic.EvaluateClient();
        }

        // can be called server side but only takes effect client side
        public void SetSubmitEnabled(bool enabled, string note)
        {
            OnClientSubmitEnabledChanged?.Invoke(enabled, note);
        }

        // can be called server side but only takes effect client side
        public void SetMissionStatus(string status)
        {
            MyLog.Default.Info($"[HnzCoopSeason] mission status: {status.Length} letters");
            OnClientMissionStatusChanged?.Invoke(status);
        }

        public void SendSubmissionToServer()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);
            MyLog.Default.Info("[HnzCoopSeason] MissionClient submitting");

            var payload = new SubmitPayload
            {
                PlayerId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId,
                MissionIndex = _selectedMissionLogic.Mission.Index,
            };

            var bytes = MyAPIGateway.Utilities.SerializeToBinary(payload);
            _submitMessenger.SendToServer(bytes);
        }

        void OnServerReceivedSubmission(ulong senderId, byte[] bytes)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Info($"[HnzCoopSeason] MissionService.OnServerReceivedSubmission({senderId})");

            var payload = MyAPIGateway.Utilities.SerializeFromBinary<SubmitPayload>(bytes);
            var missionIndex = payload.MissionIndex;
            var playerId = payload.PlayerId;

            try
            {
                if (missionIndex != CurrentMissionIndex)
                {
                    MyLog.Default.Error($"[HnzCoopSeason] invalid mission index: {missionIndex}; now: {CurrentMissionIndex}");
                    return;
                }

                var player = MyAPIGateway.Players.TryGetIdentityId(playerId);
                var logic = CreateLogic(_missions[CurrentMissionIndex], player);
                if (!logic.TrySubmit())
                {
                    MyLog.Default.Error("[HnzCoopSeason] unable to submit; condition unsatisfied");
                    return;
                }

                // send results back to clients
                UpdateMissions();
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] failed to submit; player: {playerId}, index: {missionIndex}, error: {e}");
            }
        }

        static IMissionLogic CreateLogic(Mission mission, IMyPlayer player)
        {
            switch (mission.Type)
            {
                case MissionType.Acquisition: return new AcquisitionMissionLogic(mission, player);
                default: throw new InvalidOperationException();
            }
        }

        [ProtoContract]
        sealed class SyncPayload
        {
            [ProtoMember(1)]
            public Mission[] Missions = Array.Empty<Mission>();

            [ProtoMember(2)]
            public int CurrentMissionIndex;

            public static SyncPayload Create(IEnumerable<Mission> missions, int currentMissionIndex) => new SyncPayload
            {
                Missions = missions.ToArray(),
                CurrentMissionIndex = currentMissionIndex,
            };
        }

        [ProtoContract]
        sealed class SubmitPayload
        {
            [ProtoMember(1)]
            public long PlayerId;

            [ProtoMember(2)]
            public int MissionIndex;

            public override string ToString()
            {
                return $"{nameof(PlayerId)}: {PlayerId}, {nameof(MissionIndex)}: {MissionIndex}";
            }
        }
    }
}