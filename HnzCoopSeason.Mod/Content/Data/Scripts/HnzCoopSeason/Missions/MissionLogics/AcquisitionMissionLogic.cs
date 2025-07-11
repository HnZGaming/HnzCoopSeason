﻿using System;
using System.Collections.Generic;
using System.Linq;
using HnzUtils;
using HnzUtils.Pools;
using Sandbox.ModAPI;
using VRage;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Utils;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMyEntity = VRage.ModAPI.IMyEntity;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace HnzCoopSeason.Missions.MissionLogics
{
    public sealed class AcquisitionMissionLogic : IMissionLogic
    {
        readonly MyItemType _itemType;
        readonly IMyPlayer _player;
        readonly List<IMyInventory> _inventories;
        IMyInventory _missionBlockInventory;
        MyFixedPoint _itemAmount;

        public AcquisitionMissionLogic(Mission mission, IMyPlayer player)
        {
            _itemType = MyItemType.Parse(mission.CustomData[MissionUtils.AcquisitionItemTypeKey]);
            _player = player;
            _inventories = new List<IMyInventory>();
            Mission = mission;
        }

        string SaveKey => $"AcquisitionMission/{Mission.Index}";
        public Mission Mission { get; }

        void IMissionLogic.LoadState()
        {
            Mission.Progress = GetProgress();
        }

        void IMissionLogic.OnClientUpdate()
        {
            if (Mission.Progress >= Mission.Goal)
            {
                MissionService.Instance.SetSubmitEnabled(false, MissionUtils.MissionCleared);
                return;
            }

            if (Mission.Index != MissionService.Instance.CurrentMissionIndex)
            {
                MissionService.Instance.SetSubmitEnabled(false, MissionUtils.NotCurrentMission);
                return;
            }

            MissionBlock missionBlock;
            if (!MissionBlock.TryFindNearby(_player, out missionBlock))
            {
                MissionService.Instance.SetSubmitEnabled(false, MissionUtils.MissionBlockFar);
                return;
            }

            if (_itemAmount == 0)
            {
                MissionService.Instance.SetSubmitEnabled(false, "No items in inventories");
                return;
            }

            MissionService.Instance.SetSubmitEnabled(true, $"Submitting [ {_itemAmount} ] units");
        }

        void IMissionLogic.EvaluateClient()
        {
            const string StatusFormat = @"
Item type: {0}
Total: {1} units
Acquired: {2} units
In Demand: [ {3} ] units
";
            MissionService.Instance.SetMissionStatus(string.Format(StatusFormat,
                _itemType.SubtypeId, Mission.Goal, Mission.Progress, Mission.RemainingProgress));

            MissionBlock missionBlock;
            if (!MissionBlock.TryFindNearby(_player, out missionBlock)) return;

            Evaluate(missionBlock);
        }

        void Evaluate(MissionBlock missionBlock)
        {
            _itemAmount = 0;
            _inventories.Clear();
            _missionBlockInventory = missionBlock.Entity.GetInventory();

            //count items in player inventory
            var character = _player?.Character;
            if (character != null && character.HasInventory)
            {
                EvaluateEntity(character, false);
            }

            //count items in connected grids
            List<IMyTerminalBlock> blocks;
            using (ListPool<IMyTerminalBlock>.Instance.GetUntilDispose(out blocks))
            {
                var grid = (IMyCubeGrid)missionBlock.Entity.GetTopMostParent(typeof(IMyCubeGrid));
                var terminal = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
                terminal.GetBlocks(blocks);

                var groups = blocks
                    .Select((item, index) => new { item, index })
                    .GroupBy(x => x.index / 1000)
                    .Select(g => g.Select(x => x.item));

                MyAPIGateway.Parallel.ForEach(groups, group =>
                {
                    foreach (var block in group)
                    {
                        EvaluateEntity(block, true);
                    }
                });
            }

            MyLog.Default.Info($"[HnzCoopSeason] AcquisitionMissionLogic.Evaluate() done; item amount: {_itemAmount}");
        }

        void EvaluateEntity(IMyEntity entity, bool checkConnection)
        {
            if (!entity.HasInventory) return;

            for (var i = 0; i < entity.InventoryCount; i++)
            {
                var inventory = entity.GetInventory(i);
                var item = inventory.FindItem(_itemType);
                if (item == null) continue;
                if (checkConnection && !_missionBlockInventory.CanTransferItemTo(inventory, _itemType)) continue;

                _itemAmount += item.Value.Amount;
                _inventories.Add(inventory);
                MyLog.Default.Info($"[HnzCoopSeason] AcquisitionMissionLogic item count: {_itemAmount}");
            }
        }

        bool IMissionLogic.TrySubmit()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Info("[HnzCoopSeason] AcquisitionMissionLogic.TrySubmit()");

            MissionBlock missionBlock;
            if (!MissionBlock.TryFindNearby(_player, out missionBlock))
            {
                return false;
            }

            Evaluate(missionBlock);

            var targetAmount = Math.Min(_itemAmount.ToIntSafe(), Mission.RemainingProgress);
            var pile = targetAmount;
            foreach (var inventory in _inventories)
            {
                var item = inventory.FindItem(_itemType);
                if (item == null) continue; // shouldn't happen

                var amount = item.Value.Amount.ToIntSafe();
                var subtractedAmount = Math.Min(amount, pile);
                inventory.RemoveItems(item.Value.ItemId, subtractedAmount);
                pile -= subtractedAmount;
                MyLog.Default.Info($"[HnzCoopSeason] AcquisitionMissionLogic processing submit: {inventory.Owner}, {amount}, {subtractedAmount}, {pile}");

                if (pile <= 0) break;
            }

            if (pile > 0)
            {
                MyLog.Default.Error("[HnzCoopSeason] AcquisitionMissionLogic failed to submit");
                return false;
            }

            var currentProgress = GetProgress();
            var newProgress = currentProgress + targetAmount;
            MyLog.Default.Info($"[HnzCoopSeason] AcquisitionMissionLogic progress {currentProgress} -> {newProgress}");

            SetProgress(newProgress);
            return true;
        }

        void IMissionLogic.ForceProgress(int progress)
        {
            SetProgress(progress);
        }

        int GetProgress()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);

            int currentProgress;
            if (!MyAPIGateway.Utilities.GetVariable(SaveKey, out currentProgress))
            {
                currentProgress = 0;
            }

            return currentProgress;
        }

        void SetProgress(int progress)
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyAPIGateway.Utilities.SetVariable(SaveKey, progress);
        }
    }
}