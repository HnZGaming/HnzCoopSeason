using System;
using System.Collections.Generic;
using HnzCoopSeason.Missions.MissionLogics;
using Sandbox.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.Missions
{
    public sealed class MissionClient
    {
        public static readonly MissionClient Instance = new MissionClient();

        readonly Dictionary<long, Mission> _missions = new Dictionary<long, Mission>();
        IMissionLogic _selectedMission;

        public void Load()
        {
            MissionService.Instance.OnMissionsUpdated += OnMissionsUpdated;
        }

        public void Unload()
        {
            MissionService.Instance.OnMissionsUpdated -= OnMissionsUpdated;
        }

        void OnMissionsUpdated(Mission[] missions)
        {
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
                _selectedMission = CreateClientMissionLogic(mission);

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
            MissionService.Instance.Submit(_selectedMission.Mission.Id);
        }

        static IMissionLogic CreateClientMissionLogic(Mission mission)
        {
            var player = MyAPIGateway.Session.LocalHumanPlayer;
            switch (mission.Type)
            {
                case MissionType.Acquisition: return new AcquisitionMissionLogic(mission, player);
                default: throw new InvalidOperationException();
            }
        }

        static bool TryFindMissionBlockNearby(out MissionBlock missionBlock)
        {
            missionBlock = null;

            var character = MyAPIGateway.Session.Player?.Character;
            if (character == null) return false;

            return MissionUtils.TryGetMissionBlockNearby(character, out missionBlock);
        }
    }
}