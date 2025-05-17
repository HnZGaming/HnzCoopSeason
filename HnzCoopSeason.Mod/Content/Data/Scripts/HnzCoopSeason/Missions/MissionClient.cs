using System;
using System.Collections.Generic;
using HnzCoopSeason.Missions.MissionLogics;
using HnzCoopSeason.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Missions
{
    public sealed class MissionClient
    {
        public static readonly MissionClient Instance = new MissionClient();
        readonly Dictionary<long, Mission> _missions = new Dictionary<long, Mission>();
        NetworkMessenger _submitMessenger;
        NetworkMessenger _queryMessenger;
        IMissionLogic _selectedMission;

        public void Load()
        {
            _submitMessenger = new NetworkMessenger("HnzCoopSeason.Missions.MissionClient.Submit", OnSubmitMessageReceived);
            _submitMessenger.Load();

            _queryMessenger = new NetworkMessenger("HnzCoopSeason.Missions.MissionClient.Query", OnQueryMessageReceived);
            _queryMessenger.Load();
        }

        public void Unload()
        {
            _submitMessenger.Unload();
            _queryMessenger.Unload();
        }

        public void UpdateMissions(Mission[] missions)
        {
            MyLog.Default.Info("[HnzCoopSeason] MissionClient.UpdateMissions()");
            
            _missions.Clear();
            foreach (var mission in missions)
            {
                _missions.Add(mission.Id, mission);
            }

            MissionWindow.Instance.UpdateMissionList(missions);
        }

        public void Update()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 10 != 0) return;
            if (MissionWindow.Instance == null) return;
            if (!MissionWindow.Instance.Visible) return;
            if (_selectedMission == null) return;

            MissionBlock missionBlock;
            TryFindMissionBlockNearby(out missionBlock);
            _selectedMission.Update(missionBlock);

            MissionWindow.Instance.SetSubmitEnabled(_selectedMission.CanSubmit);
            MissionWindow.Instance.SetSubmitNote(_selectedMission.SubmitNote);
        }

        public void SelectMission(long missionId)
        {
            if (missionId == 0)
            {
                _selectedMission = null;
                MissionWindow.Instance.OnMissionSelected(null);
                return;
            }

            try
            {
                var mission = _missions[missionId];
                var player = MyAPIGateway.Session.LocalHumanPlayer;
                _selectedMission = MissionUtils.CreateClientMissionLogic(mission, player);

                MissionBlock missionBlock;
                TryFindMissionBlockNearby(out missionBlock);
                _selectedMission.UpdateFull(missionBlock);

                MissionWindow.Instance.OnMissionSelected(mission);
                MissionWindow.Instance.SetStatus(_selectedMission.Status);
                MissionWindow.Instance.SetSubmitEnabled(_selectedMission.CanSubmit);
                MissionWindow.Instance.SetSubmitNote(_selectedMission.SubmitNote);
            }
            catch (Exception e)
            {
                _selectedMission = null;
                MissionWindow.Instance.OnMissionSelected(null);
                MyLog.Default.Error($"[HnzCoopSeason] {e}");
            }
        }

        public void Submit()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediClient | NetworkType.SinglePlayer);
            MyLog.Default.Info("[HnzCoopSeason] MissionClient submitting");

            var playerId = MyAPIGateway.Session.LocalHumanPlayer.IdentityId;
            var missionId = _selectedMission.Mission.Id;
            var level = _selectedMission.Mission.Level;
            var payload = new SubmitPayload(playerId, level, missionId);
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(payload);
            _submitMessenger.SendToServer(bytes);
        }

        void OnSubmitMessageReceived(ulong senderId, byte[] bytes)
        {
            var payload = MyAPIGateway.Utilities.SerializeFromBinary<SubmitPayload>(bytes);
            MissionService.Instance.Submit(payload.MissionId, payload.Level, payload.PlayerId);
        }

        public void RequestUpdate()
        {
            MyLog.Default.Info("[HnzCoopSeason] MissionClient requesting update");
            _queryMessenger.SendToServer(Array.Empty<byte>());
        }

        void OnQueryMessageReceived(ulong senderId, byte[] bytes)
        {
            MissionService.Instance.SendMissionsToClients(senderId);
        }

        static bool TryFindMissionBlockNearby(out MissionBlock missionBlock)
        {
            missionBlock = null;

            var character = MyAPIGateway.Session.Player?.Character;
            if (character == null) return false;

            return MissionUtils.TryGetMissionBlockNearby(character, out missionBlock);
        }

        [ProtoContract]
        sealed class SubmitPayload
        {
            [ProtoMember(1)]
            public long PlayerId;

            [ProtoMember(2)]
            public int Level;

            [ProtoMember(3)]
            public int MissionId;

            public SubmitPayload()
            {
            }

            public SubmitPayload(long playerId, int level, int missionId)
            {
                PlayerId = playerId;
                Level = level;
                MissionId = missionId;
            }

            public override string ToString()
            {
                return $"{nameof(PlayerId)}: {PlayerId}, {nameof(Level)}: {Level}, {nameof(MissionId)}: {MissionId}";
            }
        }
    }
}