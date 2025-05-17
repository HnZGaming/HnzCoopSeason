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
        static readonly ushort ModKey = (ushort)"HnzCoopSeason.Missions.MissionService".GetHashCode();

        List<Mission> _missions;

        public void Load()
        {
            _missions = new List<Mission>();
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ModKey, OnMessageReceived);
        }

        public void Unload()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ModKey, OnMessageReceived);
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

        public void Submit(long missionId)
        {
            //todo impl
        }

        void SendMissionsToClients()
        {
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(_missions);

            switch (VRageUtils.NetworkType)
            {
                case NetworkType.DediServer:
                    MyAPIGateway.Multiplayer.SendMessageToOthers(ModKey, bytes);
                    break;
                case NetworkType.DediClient:
                    throw new InvalidOperationException();
                case NetworkType.SinglePlayer:
                    OnMessageReceived(ModKey, bytes, 0, false);
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        void OnMessageReceived(ushort modKey, byte[] bytes, ulong senderId, bool fromServer)
        {
            if (modKey != ModKey) return;
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