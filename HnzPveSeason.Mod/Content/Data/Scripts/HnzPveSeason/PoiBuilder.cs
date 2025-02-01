using System;
using HnzPveSeason.Utils;
using ProtoBuf;
using VRage.Utils;

namespace HnzPveSeason
{
    [ProtoContract]
    public sealed class PoiBuilder
    {
        [ProtoMember(1)]
        public PoiState CurrentState;

        static string GetVariableKey(string id)
        {
            return $"HnzPveSeason.Poi.{id}";
        }

        public static bool TryLoad(string id, out PoiBuilder builder)
        {
            try
            {
                var variableKey = GetVariableKey(id);
                return VRageUtils.TryLoadProtobufVariable(variableKey, out builder);
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzPveSeason] poi `{id}` builder failed to parse: {e}");
                builder = null;
                return false;
            }
        }

        public static void Save(string id, PoiBuilder builder)
        {
            var variableKey = GetVariableKey(id);
            VRageUtils.SaveProtobufVariable(variableKey, builder);
        }
    }
}