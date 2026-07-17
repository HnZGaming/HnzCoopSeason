using System.Collections.Generic;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.Orks
{
    public static class OrkUtils
    {
        // hp multiplier a grid of the given progress level should carry, at this point of the season
        public static float ComputeHpMultiplier(int level)
        {
            var c = SessionConfig.Instance.GetProgressionLevel(level);
            var t = Session.Instance.GetProgressLevelFraction(level);
            return MathHelper.Lerp(c.HpMultiplierStart, c.HpMultiplierEnd, t);
        }

        public static int DisarmGrid(IMyCubeGrid grid)
        {
            var count = 0;

            // rival ai brain: the MES behavior lives on the remote control and keeps flying/taunting even with
            // autopilot off (and MyRemoteControl is NOT an IMyFunctionalBlock -- a direct cast crashes the game).
            // Removing the block kills the behavior outright; dampeners then brake the grid to a stop.
            var remotes = new List<IMyRemoteControl>();
            foreach (var block in grid.GetFatBlocks<IMyRemoteControl>())
            {
                remotes.Add(block);
            }

            foreach (var block in remotes)
            {
                block.SetAutoPilotEnabled(false);
                grid.RemoveBlock(block.SlimBlock, true);
                count += 1;
            }

            foreach (var block in grid.GetFatBlocks<IMyUserControllableGun>())
            {
                block.Enabled = false;
                count += 1;
            }

            foreach (var block in grid.GetFatBlocks<IMyConveyorSorter>())
            {
                block.Enabled = false; // weaponcore sorter-weapons
                count += 1;
            }

            // static weapons fired by controllers rather than their own AI
            foreach (var block in grid.GetFatBlocks<IMyTurretControlBlock>())
            {
                block.Enabled = false;
                count += 1;
            }

            foreach (var block in grid.GetFatBlocks<IMyOffensiveCombatBlock>())
            {
                block.Enabled = false; // automatons combat ai
                count += 1;
            }

            foreach (var block in grid.GetFatBlocks<IMyDefensiveCombatBlock>())
            {
                block.Enabled = false;
                count += 1;
            }

            foreach (var block in grid.GetFatBlocks<IMyFlightMovementBlock>())
            {
                block.Enabled = false;
                count += 1;
            }

            foreach (var block in grid.GetFatBlocks<IMyWarhead>())
            {
                block.IsArmed = false;
                count += 1;
            }

            return count;
        }
    }
}
