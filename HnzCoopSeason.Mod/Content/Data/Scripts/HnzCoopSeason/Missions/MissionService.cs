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
            _missionMessenger = new NetworkMessenger("HnzCoopSeason.Missions.MissionService.Sync", OnMissionsReceived);
            _missionMessenger.Load();
        }

        public void Unload()
        {
            _missionMessenger.Unload();
        }

        public void UpdateMissions() // server
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            
            //todo evaluate the current level

            var level = Session.Instance.GetProgressLevel();
            var missions = ReadMissions(level);
            _missions.Clear();
            _missions.AddRange(missions);

            SendMissionsToPlayers();
        }

        public void UpdateMission(int level, int missionId, int progress)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Info($"[HnzCoopSeason] MissionService.UpdateMission({level}, {missionId}, {progress})");

            WriteMissionState(level, missionId, progress);
            UpdateMissions();
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

                logic.ProcessSubmit();
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] failed to submit; error: {e}");
            }
        }

        public void SendMissionsToPlayers(ulong? receiverIdOrEveryone = null)
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(_missions);
            if (receiverIdOrEveryone.HasValue)
            {
                // --> OnMissionsReceived()
                _missionMessenger.SendTo(receiverIdOrEveryone.Value, bytes);
            }
            else
            {
                // --> OnMissionsReceived()
                _missionMessenger.SendToOthers(bytes);
            }
        }

        void OnMissionsReceived(ulong steamId, byte[] bytes)
        {
            var missions = MyAPIGateway.Utilities.SerializeFromBinary<Mission[]>(bytes);
            MissionClient.Instance.UpdateMissions(missions);
        }

        public static List<Mission> ReadMissions(int level)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);

            var missions = new List<Mission>();
            ProgressionLevelConfig levelConfig;
            if (!SessionConfig.Instance.ProgressionLevels.TryGetValue(level, out levelConfig)) return missions;

            for (var i = 0; i < levelConfig.Missions.Length; i++)
            {
                var c = levelConfig.Missions[i];
                var id = i + 1;
                var progress = ReadMissionState(level, id);
                var mission = new Mission(c, level, id, progress);
                missions.Add(mission);
            }

            return missions;
        }

        static int ReadMissionState(int level, int id)
        {
            int progress;
            var key = StorageVariableKey(level, id);
            MyAPIGateway.Utilities.GetVariable(key, out progress);
            return progress;
        }

        static void WriteMissionState(int level, int id, int progress)
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