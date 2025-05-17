using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils;
using HnzCoopSeason.Utils.Pools;
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
        public string SubmitNote { get; private set; }
        public string Status { get; private set; }
        public int DeltaProgress => Math.Min(_itemAmount.ToIntSafe(), Mission.RemainingProgress);

        public void Update(MissionBlock missionBlockOrNotFound)
        {
            _missionBlock = missionBlockOrNotFound;
            UpdateState();
        }

        public void UpdateFull(MissionBlock missionBlockOrNotFound)
        {
            _itemAmount = 0;
            Status = $"Status:\n - {_itemType.SubtypeId} [ {Mission.TotalProgress - Mission.Progress} ] units (total: {Mission.TotalProgress} units)";

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
                MyLog.Default.Info($"[HnzCoopSeason] item count: {_itemAmount}");
            }
        }

        void UpdateState()
        {
            if (_missionBlock == null)
            {
                CanSubmit = false;
                SubmitNote = MissionUtils.MissionBlockFar;
                return;
            }

            if (_itemAmount == 0)
            {
                CanSubmit = false;
                SubmitNote = "No items in found in inventories";
                return;
            }

            CanSubmit = true;
            SubmitNote = $"Submitting [ {_itemAmount} ] units";
        }

        public bool TryProcessSubmit()
        {
            VRageUtils.AssertNetworkType(NetworkType.DediServer | NetworkType.SinglePlayer);
            MyLog.Default.Error($"[HnzCoopSeason] AcquisitionMissionLogic.ProcessSubmit(); inventories: {_inventories.Count}");

            var remainingAmount = DeltaProgress;
            foreach (var inventory in _inventories)
            {
                var item = inventory.FindItem(_itemType);
                if (item == null) continue; // shouldn't happen

                var amount = item.Value.Amount.ToIntSafe();
                var subtractedAmount = Math.Min(amount, remainingAmount);
                inventory.RemoveItems(item.Value.ItemId, subtractedAmount);
                remainingAmount -= subtractedAmount;
                MyLog.Default.Info($"[HnzCoopSeason] Acquisition Mission processing submit: {inventory.Owner}, {amount}, {subtractedAmount}, {remainingAmount}");

                if (remainingAmount < 0) break;
            }

            return remainingAmount == 0;
        }
    }
}