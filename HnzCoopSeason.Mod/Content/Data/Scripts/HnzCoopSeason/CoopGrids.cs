using System;
using System.Collections.Generic;
using HnzCoopSeason.Utils;
using HnzCoopSeason.Utils.Pools;
using Sandbox.ModAPI;
using VRage.Game;
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

        public struct Analysis
        {
            public IMyCubeGrid Grid;
            public Owner Owner;
            public MyRelationsBetweenFactions Relation;
            public int SpawnGroupIndex;
            public string FactionTag;

            public bool IsOrksLeader => SpawnGroupIndex == 0 && FactionTag == "PORKS";
        }

        public static Analysis Analyze(IMyCubeGrid grid, long playerId = 0)
        {
            var analysis = default(Analysis);
            analysis.Grid = grid;

            if (grid.BigOwners.Count == 0)
            {
                analysis.Owner = Owner.Nobody;
            }
            else
            {
                var ownerId = grid.BigOwners[0];
                analysis.Owner = GetOwnerType(ownerId);
                analysis.FactionTag = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId)?.Tag;

                if (playerId != 0)
                {
                    analysis.Relation = GetFactionRelation(ownerId, playerId);
                }
            }

            MesGridContext context;
            if (MesGridGroup.TryGetSpawnContext(grid, out context))
            {
                analysis.SpawnGroupIndex = context.Index;
            }

            return analysis;
        }

        static Owner GetOwnerType(long ownerId)
        {
            if (ownerId == 0) return Owner.Nobody;
            return VRageUtils.IsNpc(ownerId) ? Owner.NPC : Owner.Player;
        }

        static MyRelationsBetweenFactions GetFactionRelation(long u1, long u2)
        {
            if (u1 == u2) return MyRelationsBetweenFactions.Friends;

            var u1faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(u1);
            if (u1faction?.Members.ContainsKey(u2) ?? false) return MyRelationsBetweenFactions.Friends;

            var u2faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(u2);
            return MyAPIGateway.Session.Factions.GetRelationBetweenFactions(u1faction?.FactionId ?? 0, u2faction?.FactionId ?? 0);
        }

        public static bool GetTakeoverProgress(IMyCubeGrid grid, bool countAll, out int successCount, out int totalCount)
        {
            totalCount = successCount = 0;

            if (grid.BigOwners.Count == 0) return true;

            var ownerId = grid.BigOwners[0];
            if (GetOwnerType(ownerId) != Owner.NPC) return true;

            List<IMyTerminalBlock> blocks;
            using (ListPool<IMyTerminalBlock>.Instance.GetUntilDispose(out blocks))
            {
                blocks.AddRange(grid.GetFatBlocks<IMyRemoteControl>());
                blocks.AddRange(grid.GetFatBlocks<IMyCockpit>());
                foreach (var block in blocks)
                {
                    totalCount += 1;
                    if (block.OwnerId == 0 || !VRageUtils.IsNpc(block.OwnerId)) // unowned or of player
                    {
                        successCount += 1;
                    }
                    else if (!countAll)
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