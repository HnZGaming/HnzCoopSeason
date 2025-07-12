using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace HnzUtils
{
    public static class VRageUtils
    {
        public static NetworkType NetworkType
        {
            get
            {
                if (MyAPIGateway.Utilities.IsDedicated) return NetworkType.DediServer;
                return MyAPIGateway.Session.IsServer ? NetworkType.SinglePlayer : NetworkType.DediClient;
            }
        }

        public static bool NetworkTypeIn(NetworkType networkTypes)
        {
            return (networkTypes & NetworkType) == NetworkType;
        }

        public static void AssertNetworkType(NetworkType networkTypes, string message = "invalid network type")
        {
            if (!NetworkTypeIn(networkTypes))
            {
                throw new InvalidOperationException(message);
            }
        }

        public static bool IsNpc(long identityId)
        {
            return MyAPIGateway.Players.TryGetSteamId(identityId) == 0;
        }

        public static GridOwnerType GetOwnerType(long ownerId)
        {
            if (ownerId == 0) return GridOwnerType.Nobody;
            return IsNpc(ownerId) ? GridOwnerType.NPC : GridOwnerType.Player;
        }

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

        public static bool TryGetCharacter(ulong steamId, out IMyCharacter character)
        {
            var playerId = MyAPIGateway.Players.TryGetIdentityId(steamId);
            character = MyAPIGateway.Players.TryGetIdentityId(playerId)?.Character;
            return character != null;
        }

        public static IMyCharacter TryGetCharacter(long playerId)
        {
            return MyAPIGateway.Players.TryGetIdentityId(playerId)?.Character;
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

        public static string FormatGps(string name, Vector3D position, string colorCode)
        {
            // example -- GPS:1-1-2:-5000000:-5000000:0:#FF75C9F1:
            return $"GPS:{name}:{position.X}:{position.Y}:{position.Z}:#{colorCode}";
        }

        public static bool TryGetVoxelIntersection(LineD line, IEnumerable<MyVoxelBase> voxels, out Vector3D intersection)
        {
            foreach (var v in voxels)
            {
                Vector3D? i;
                if (v.GetIntersectionWithLine(ref line, out i) && i != null)
                {
                    intersection = i.Value;
                    return true;
                }
            }

            intersection = Vector3D.Zero;
            return false;
        }

        public static bool IsInAnySafeZone(long entityId)
        {
            foreach (var zone in MySessionComponentSafeZones.SafeZones)
            {
                if (MySessionComponentSafeZones.IsInSafezone(entityId, zone))
                {
                    return true;
                }
            }

            return false;
        }

        public static void PlaySound(string cueName)
        {
            var character = MyAPIGateway.Session?.LocalHumanPlayer?.Character;
            if (character == null) return;

            var emitter = new MyEntity3DSoundEmitter(character as MyEntity);
            var sound = new MySoundPair(cueName);
            emitter.PlaySound(sound);
        }
    }
}