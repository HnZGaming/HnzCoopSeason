using System;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
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

        public static bool TryLoadProtobufVariable<T>(string key, out T value)
        {
            string dataStr;
            if (!MyAPIGateway.Utilities.GetVariable(key, out dataStr))
            {
                value = default(T);
                return false;
            }

            var data = Convert.FromBase64String(dataStr);
            value = MyAPIGateway.Utilities.SerializeFromBinary<T>(data);
            return true;
        }

        public static void SaveProtobufVariable<T>(string key, T value)
        {
            var data = MyAPIGateway.Utilities.SerializeToBinary(value);
            var dataStr = Convert.ToBase64String(data);
            MyAPIGateway.Utilities.SetVariable(key, dataStr);
        }

        public static Vector3 CalculateNaturalGravity(Vector3 point)
        {
            float _;
            return MyAPIGateway.Physics.CalculateNaturalGravityAt(point, out _);
        }
    }
}