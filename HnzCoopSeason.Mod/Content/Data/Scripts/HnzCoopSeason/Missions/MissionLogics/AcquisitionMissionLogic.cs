using System;
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
        readonly IMyPlayer _player;
        readonly MyItemType _itemType;
        readonly List<IMyInventory> _inventories;
        MissionBlock _missionBlock;
        IMyInventory _missionBlockInventory;
        MyFixedPoint _itemAmount;

        public AcquisitionMissionLogic(Mission mission, IMyPlayer player)
        {
            _player = player;
            _itemType = MyItemType.Parse(mission.AcquisitionItemType);
            _inventories = new List<IMyInventory>();
            Mission = mission;
        }

        public Mission Mission { get; }
        public bool CanSubmit { get; private set; }
        public string SubmitNoteText { get; private set; }
        public string StatusText { get; private set; }

        public void Update(MissionBlock missionBlockOrNotFound)
        {
            _missionBlock = missionBlockOrNotFound;
            UpdateState();
        }

        public void UpdateFull(MissionBlock missionBlockOrNotFound)
        {
            _itemAmount = 0;
            StatusText = $@"
Item type: {_itemType.SubtypeId}
Total: {Mission.Goal} units in demand
Status: {Mission.Progress} units submitted, [ {Mission.RemainingProgress} ] units in demand
";

            _missionBlock = missionBlockOrNotFound;
            if (_missionBlock == null) return;

            _inventories.Clear();
            _missionBlockInventory = _missionBlock.Entity.GetInventory();

            //count items in player inventory
            var character = _player.Character;
            if (character != null && character.HasInventory)
            {
                CountItems(character, false);
            }

            //count items in connected grids
            List<IMyTerminalBlock> blocks;
            using (ListPool<IMyTerminalBlock>.Instance.GetUntilDispose(out blocks))
            {
                var grid = (IMyCubeGrid)_missionBlock.Entity.GetTopMostParent(typeof(IMyCubeGrid));
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
                        CountItems(block, true);
                    }
                });
            }

            UpdateState();
        }

        void CountItems(IMyEntity entity, bool checkConnection)
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

        void UpdateState()
        {
            if (_missionBlock == null)
            {
                CanSubmit = false;
                SubmitNoteText = MissionUtils.MissionBlockFar;
                return;
            }

            if (_itemAmount == 0)
            {
                CanSubmit = false;
                SubmitNoteText = "No items in found in inventories";
                return;
            }

            CanSubmit = true;
            SubmitNoteText = $"Submitting [ {_itemAmount} ] units";
        }

        public void ProcessSubmit()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Error($"[HnzCoopSeason] AcquisitionMissionLogic.ProcessSubmit(); inventories: {_inventories.Count}");

            var deltaProgress = Math.Min(_itemAmount.ToIntSafe(), Mission.RemainingProgress);
            var remainingAmount = deltaProgress;
            foreach (var inventory in _inventories)
            {
                var item = inventory.FindItem(_itemType);
                if (item == null) continue; // shouldn't happen

                var amount = item.Value.Amount.ToIntSafe();
                var subtractedAmount = Math.Min(amount, remainingAmount);
                inventory.RemoveItems(item.Value.ItemId, subtractedAmount);
                remainingAmount -= subtractedAmount;
                MyLog.Default.Info($"[HnzCoopSeason] AcquisitionMissionLogic processing submit: {inventory.Owner}, {amount}, {subtractedAmount}, {remainingAmount}");

                if (remainingAmount < 0) break;
            }

            if (remainingAmount > 0)
            {
                MyLog.Default.Error("[HnzCoopSeason] AcquisitionMissionLogic failed to submit");
                return;
            }

            var newProgress = Mission.Progress + deltaProgress;
            MissionService.Instance.UpdateMission(Mission.Level, Mission.Id, newProgress);
        }
    }
}