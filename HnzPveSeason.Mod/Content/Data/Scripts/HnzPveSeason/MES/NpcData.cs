using System;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace HnzPveSeason.MES
{
    [ProtoContract]
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class NpcData
    {
        static readonly Guid Key = new Guid("29FFD684-13D7-4045-BF76-CD48BF80E36A"); // copied from mes

        [ProtoMember(42)]
        public string Context; // arbitrary user data via MESApi

        public static bool TryGetNpcData(IMyCubeGrid grid, out NpcData npcData)
        {
            npcData = null;
            if (grid.Storage == null) return false;

            string value;
            if (!grid.Storage.TryGetValue(Key, out value)) return false;

            var bytes = Convert.FromBase64String(value);
            npcData = MyAPIGateway.Utilities.SerializeFromBinary<NpcData>(bytes);
            return true;
        }

        public static void RemoveNpcData(IMyCubeGrid grid)
        {
            grid.Storage?.Remove(Key);
        }
    }
}