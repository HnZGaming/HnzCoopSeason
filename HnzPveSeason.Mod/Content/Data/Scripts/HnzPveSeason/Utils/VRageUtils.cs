using System;
using System.Collections.Generic;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace HnzPveSeason.Utils
{
    public static class VRageUtils
    {
        public static void UpdateStorageValue(this IMyEntity entity, Guid key, string value)
        {
            var storage = entity.Storage ?? new MyModStorageComponent();
            entity.Storage = storage;
            storage.SetValue(key, value);
        }

        public static bool TryGetStorageValue(this IMyEntity entity, Guid key, out string value)
        {
            if (entity.Storage == null)
            {
                value = null;
                return false;
            }

            return entity.Storage.TryGetValue(key, out value);
        }

        public static Vector3 CalculateNaturalGravity(Vector3 point)
        {
            float _;
            return MyAPIGateway.Physics.CalculateNaturalGravityAt(point, out _);
        }

        public static bool IsGridControlledByAI(IMyCubeGrid grid)
        {
            var terminalSystems = MyAPIGateway.TerminalActionsHelper.GetTerminalSystemForGrid(grid);
            var controlBlocks = new List<IMyTerminalBlock>();
            terminalSystems.GetBlocksOfType<IMyRemoteControl>(controlBlocks);
            terminalSystems.GetBlocksOfType<IMyCockpit>(controlBlocks);
            foreach (var block in controlBlocks)
            {
                if (MyAPIGateway.Players.TryGetSteamId(block.OwnerId) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static void AddTemporaryGps(string name, Color color, double discardAt, Vector3D coords)
        {
            var gps = MyAPIGateway.Session.GPS.Create(name, "", coords, true, true);
            gps.GPSColor = color;
            gps.DiscardAt = TimeSpan.FromSeconds(discardAt);
            gps.UpdateHash();
            MyAPIGateway.Session.GPS.AddLocalGps(gps);
        }

        public static bool TryGetCharacter(ulong steamId, out IMyCharacter character)
        {
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            character = MyAPIGateway.Players.TryGetIdentityId(playerId)?.Character;
            return character != null;
        }

        public static bool TryGetFaction(long blockId, out IMyFaction faction)
        {
            faction = null;

            IMyEntity entity;
            if (!MyAPIGateway.Entities.TryGetEntityById(blockId, out entity)) return false;

            var block = entity as IMyCubeBlock;
            if (block == null) return false;

            var ownerId = block.OwnerId;
            faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
            return faction != null;
        }
    }
}