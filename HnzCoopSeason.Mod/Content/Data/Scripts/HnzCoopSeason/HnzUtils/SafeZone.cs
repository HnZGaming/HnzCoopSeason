using HnzUtils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.HnzUtils
{
    public sealed class SafeZone
    {
        readonly string _id;

        public SafeZone(string id)
        {
            _id = id;
        }

        public long SafezoneId { get; set; }

        public void Create(Vector3D position, float radius)
        {
            MySafeZone safezone;
            if (VRageUtils.TryGetEntityById(SafezoneId, out safezone))
            {
                Remove();
            }

            safezone = (MySafeZone)MySessionComponentSafeZones.CrateSafeZone(
                MatrixD.CreateWorld(position, Vector3D.Forward, Vector3D.Up),
                MySafeZoneShape.Sphere,
                MySafeZoneAccess.Blacklist,
                null, null, radius, true, true,
                name: $"merchant-{_id}");

            MySessionComponentSafeZones.AddSafeZone(safezone);
            SafezoneId = safezone.EntityId;
            MyLog.Default.Info($"[HnzCoopSeason] safezone {_id} created");
        }

        public void Remove()
        {
            MySafeZone safezone;
            if (!VRageUtils.TryGetEntityById(SafezoneId, out safezone)) return;

            MySessionComponentSafeZones.RemoveSafeZone(safezone);
            safezone.Close();
            MyEntities.Remove(safezone);

            SafezoneId = 0;
            MyLog.Default.Info($"[HnzCoopSeason] safezone {_id} removed");
        }
    }
}