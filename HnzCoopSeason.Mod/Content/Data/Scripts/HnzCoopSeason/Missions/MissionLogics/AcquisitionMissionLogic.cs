using System.Collections.Generic;
using System.Linq;
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
        MissionBlock _missionBlock;
        IMyInventory _missionBlockInventory;
        MyFixedPoint _itemCount;

        public AcquisitionMissionLogic(Mission mission, IMyPlayer player)
        {
            _player = player;
            _itemType = MyItemType.Parse(mission.AcquisitionItemType);
            Mission = mission;
        }

        public Mission Mission { get; }
        public bool CanSubmit { get; private set; }
        public string SubmitNote { get; private set; }
        public string Status { get; private set; }

        public void Update(MissionBlock missionBlockOrNotFound)
        {
            _missionBlock = missionBlockOrNotFound;
            UpdateState();
        }

        public void UpdateFull(MissionBlock missionBlockOrNotFound)
        {
            _itemCount = 0;
            Status = $"Status:\n - {_itemType.SubtypeId} [ {Mission.TotalProgress - Mission.Progress} ] units (total: {Mission.TotalProgress} units)";

            _missionBlock = missionBlockOrNotFound;
            if (_missionBlock == null) return;

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
                        //CountItems(block, true);
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

                _itemCount += item.Value.Amount;
                MyLog.Default.Info($"[HnzCoopSeason] item count: {_itemCount}");
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

            if (_itemCount == 0)
            {
                CanSubmit = false;
                SubmitNote = "No items found";
                return;
            }

            CanSubmit = true;
            SubmitNote = $"Submitting [ {_itemCount} ] units";
        }
    }
}