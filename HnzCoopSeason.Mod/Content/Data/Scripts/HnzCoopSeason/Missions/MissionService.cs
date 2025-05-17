using System;
using System.Collections.Generic;
using HnzCoopSeason.Utils;
using Sandbox.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Missions
{
    public sealed class MissionService
    {
        public static readonly MissionService Instance = new MissionService();

        NetworkMessenger _missionMessenger;
        List<Mission> _missions;

        public void Load()
        {
            _missions = new List<Mission>();
            _missionMessenger = new NetworkMessenger("HnzCoopSeason.Missions.MissionService", OnMissionsReceived);
            _missionMessenger.Load();
        }

        public void Unload()
        {
            _missionMessenger.Unload();
        }

        public void UpdateMissionList() // server
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);

            _missions.Clear();

            var level = Session.Instance.GetProgressLevel();
            foreach (var mission in GetMissions(level))
            {
                _missions.Add(mission);
            }

            SendMissionsToClients();
        }

        public List<Mission> GetMissions(int level)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);

            var missions = new List<Mission>();
            ProgressionLevelConfig levelConfig;
            if (!SessionConfig.Instance.ProgressionLevels.TryGetValue(level, out levelConfig)) return missions;

            for (var i = 0; i < levelConfig.Missions.Length; i++)
            {
                var c = levelConfig.Missions[i];
                var id = i + 1;
                var progress = LoadMissionProgress(level, id);
                var mission = new Mission(c, level, id, progress);
                missions.Add(mission);
            }

            return missions;
        }

        public void UpdateMissionProgress(int level, int missionId, int progress)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Info($"[HnzCoopSeason] MissionService.UpdateMissionProgress({level}, {missionId}, {progress})");

            SaveMissionProgress(level, missionId, progress);

            var currentLevel = Session.Instance.GetProgressLevel();
            if (level != currentLevel) return;

            _missions[missionId - 1].Progress = progress;
            SendMissionsToClients();
        }

        public void Submit(int missionId, int level, long playerId)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);

            if (level != Session.Instance.GetProgressLevel())
            {
                MyLog.Default.Error($"[HnzCoopSeason] invalid level: {level}");
                return;
            }

            try
            {
                var mission = _missions[missionId - 1];
                var player = MyAPIGateway.Players.TryGetIdentityId(playerId);
                var logic = MissionUtils.CreateClientMissionLogic(mission, player);

                MissionBlock missionBlock;
                MissionUtils.TryGetMissionBlockNearby(player.Character, out missionBlock);
                logic.UpdateFull(missionBlock);

                if (!logic.CanSubmit)
                {
                    MyLog.Default.Error("[HnzCoopSeason] unable to submit");
                    return;
                }

                if (!logic.TryProcessSubmit())
                {
                    MyLog.Default.Error("[HnzCoopSeason] failed to submit");
                    return;
                }

                mission.Progress += logic.DeltaProgress;

                SendMissionsToClients();
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] failed to submit; error: {e}");
            }
        }

        public void SendMissionsToClients(ulong? receiverIdOrEveryone = null)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(_missions);
            if (receiverIdOrEveryone.HasValue)
            {
                _missionMessenger.SendTo(receiverIdOrEveryone.Value, bytes);
            }
            else
            {
                _missionMessenger.SendToOthers(bytes);
            }
        }

        void OnMissionsReceived(ulong steamId, byte[] bytes)
        {
            var missions = MyAPIGateway.Utilities.SerializeFromBinary<Mission[]>(bytes);
            MissionClient.Instance.UpdateMissions(missions);
        }

        static int LoadMissionProgress(int level, int id)
        {
            int progress;
            var key = StorageVariableKey(level, id);
            MyAPIGateway.Utilities.GetVariable(key, out progress);
            return progress;
        }

        static void SaveMissionProgress(int level, int id, int progress)
        {
            var key = StorageVariableKey(level, id);
            MyAPIGateway.Utilities.SetVariable(key, progress);
        }

        static string StorageVariableKey(int level, int id)
        {
            return $"mission-{level}-{id}";
        }
    }
}