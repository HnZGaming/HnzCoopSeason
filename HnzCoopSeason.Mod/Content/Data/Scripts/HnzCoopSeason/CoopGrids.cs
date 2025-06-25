using System.Collections.Generic;
using HnzUtils;
using HnzUtils.Pools;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace HnzCoopSeason
{
    public static class CoopGrids
    {
        public enum Owner
        {
            Nobody,
            NPC,
            Player,
        }

        public const string OrksFactionTag = "PORKS";
        public const string MerchantFactionTag = "MERC";

        public static Owner GetOwnerType(long ownerId)
        {
            if (ownerId == 0) return Owner.Nobody;
            return VRageUtils.IsNpc(ownerId) ? Owner.NPC : Owner.Player;
        }

        // true if takeover is successful
        //todo move to Spawners/*
        public static bool GetTakeoverProgress(IMyCubeGrid grid, bool countAll, out int successCount, out int totalCount)
        {
            LangUtils.AssertNull(grid, "grid null");

            totalCount = successCount = 0;

            // owned by nobody -> taken over
            if ((grid.BigOwners?.Count ?? 0) == 0) return true;

            // owned by player -> taken over
            var ownerId = grid.BigOwners[0];
            if (GetOwnerType(ownerId) != Owner.NPC) return true;

            List<IMyTerminalBlock> blocks;
            using (ListPool<IMyTerminalBlock>.Instance.GetUntilDispose(out blocks))
            {
                // count the number of control blocks
                blocks.AddRange(grid.GetFatBlocks<IMyRemoteControl>());
                blocks.AddRange(grid.GetFatBlocks<IMyCockpit>());
                foreach (var block in blocks)
                {
                    totalCount += 1;
                    if (block.OwnerId == 0 || !VRageUtils.IsNpc(block.OwnerId)) // owned by nobody or player
                    {
                        successCount += 1;
                    }
                    else if (!countAll) // if `out` params don't have to be accurate, we can return as soon as we know the grid isn't taken over
                    {
                        return false;
                    }
                }
            }

            return successCount == totalCount; // 0/0 -> success
        }

        public static bool IsTakenOver(IMyCubeGrid grid)
        {
            int _;
            return GetTakeoverProgress(grid, false, out _, out _);
        }
    }
}