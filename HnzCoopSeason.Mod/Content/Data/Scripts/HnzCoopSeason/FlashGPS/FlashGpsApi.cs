using System.IO;
using Sandbox.ModAPI;
using VRage;

namespace FlashGPS
{
    public static class FlashGpsApi
    {
        static readonly long ModVersion = "FlashGpsApi 1.1.*".GetHashCode();
        static long _moduleId;

        public static void Load(long moduleId)
        {
            _moduleId = moduleId;
        }

        public static void AddOrUpdate(FlashGpsSource src)
        {
            using (var stream = new ByteStream(1024))
            using (var writer = new BinaryWriter(stream))
            {
                writer.WriteAddOrUpdateFlashGps(_moduleId, src);
                MyAPIGateway.Utilities.SendModMessage(ModVersion, stream.Data);
            }
        }

        public static void Remove(long gpsId)
        {
            using (var stream = new ByteStream(1024))
            using (var writer = new BinaryWriter(stream))
            {
                writer.WriteRemoveFlashGps(_moduleId, gpsId);
                MyAPIGateway.Utilities.SendModMessage(ModVersion, stream.Data);
            }
        }
    }
}