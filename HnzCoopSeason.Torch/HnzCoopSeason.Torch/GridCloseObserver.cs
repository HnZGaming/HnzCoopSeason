using System.Diagnostics;
using System.Reflection;
using NLog;
using Sandbox.Game.Entities;
using Torch.Managers.PatchManager;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace HnzCoopSeason.Torch
{
    [PatchShim]
    // ReSharper disable once UnusedType.Global
    public static class GridCloseObserver
    {
        static readonly ILogger Log = LogManager.GetCurrentClassLogger();

        // ReSharper disable once UnusedMember.Global
        public static void Patch(PatchContext ctx)
        {
            var patchee = typeof(MyEntities).GetMethod(nameof(Close), BindingFlags.Static | BindingFlags.Public);
            var patcher = typeof(GridCloseObserver).GetMethod(nameof(Close), BindingFlags.Static | BindingFlags.NonPublic);
            ctx.GetPattern(patchee).Prefixes.Add(patcher);
        }

        static void Close(MyEntity entity)
        {
            if (entity is not IMyCubeGrid) return;

            if (entity.DisplayName?.Contains("(NPC-ORK)") ?? false)
            {
                Log.Info($"ork grid closed: {entity.DisplayName} ({entity}); stacktrace: {new StackTrace()}");
            }
        }
    }
}