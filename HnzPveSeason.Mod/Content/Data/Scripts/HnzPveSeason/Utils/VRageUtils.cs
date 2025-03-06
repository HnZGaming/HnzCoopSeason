using System;
using System.Collections.Generic;
using Sandbox.Definitions;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
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

        public static MyDefinitionId ToDefinitionId(this SerializableDefinitionId id)
        {
            return new MyDefinitionId(id.TypeId, id.SubtypeName);
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

        public static void ClearItems(this IMyStoreBlock storeBlock)
        {
            // clear existing items
            var items = new List<IMyStoreItem>();
            storeBlock.GetStoreItems(items);
            foreach (var item in items)
            {
                storeBlock.RemoveStoreItem(item);
            }
        }

        public static bool TryGetEntityById<T>(long entityId, out T entity) where T : class, IMyEntity
        {
            entity = MyAPIGateway.Entities.GetEntityById(entityId) as T;
            return entity != null;
        }

        public static bool IsContractBlock(this IMyCubeBlock block)
        {
            return block.BlockDefinition.SubtypeId?.IndexOf("ContractBlock", StringComparison.Ordinal) > -1;
        }

        public static bool IsStoreBlock(this IMyCubeBlock block)
        {
            return block is IMyStoreBlock && !(block is IMyVendingMachine);
        }

        // copied from vanilla private code
        public static int CalculateItemMinimalPrice(MyDefinitionId itemId)
        {
            MyPhysicalItemDefinition myPhysicalItemDefinition;
            if (MyDefinitionManager.Static.TryGetDefinition(itemId, out myPhysicalItemDefinition) && myPhysicalItemDefinition.MinimalPricePerUnit > 0)
            {
                return myPhysicalItemDefinition.MinimalPricePerUnit;
            }

            MyBlueprintDefinitionBase myBlueprintDefinitionBase;
            if (!MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(itemId, out myBlueprintDefinitionBase))
            {
                return 0;
            }

            var num = myPhysicalItemDefinition.IsIngot ? 1f : MyAPIGateway.Session.AssemblerEfficiencyMultiplier;
            var num2 = 0;
            foreach (var item in myBlueprintDefinitionBase.Prerequisites)
            {
                var num3 = CalculateItemMinimalPrice(item.Id);
                var num4 = (float)item.Amount / num;
                num2 += (int)(num3 * num4);
            }

            var num5 = myPhysicalItemDefinition.IsIngot ? MyAPIGateway.Session.RefinerySpeedMultiplier : MyAPIGateway.Session.AssemblerSpeedMultiplier;
            foreach (var item2 in myBlueprintDefinitionBase.Results)
            {
                if (item2.Id != itemId) continue;

                var amount = (float)item2.Amount;
                if (amount == 0f) continue;

                var num7 = 1f + (float)Math.Log(myBlueprintDefinitionBase.BaseProductionTimeInSeconds + 1f) / num5;
                var num8 = (int)(num2 * (1f / amount) * num7);
                return num8;
            }

            return 0;
        }

        public static string FormatGps(string name, Vector3D position, string colorCode)
        {
            // example -- GPS:1-1-2:-5000000:-5000000:0:#FF75C9F1:
            return $"GPS:{name}:{position.X}:{position.Y}:{position.Z}:#{colorCode}";
        }
    }
}