using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace HnzCoopSeason.HudUtils
{
    public static class WcHudApi
    {
        const long Channel = 67549756549; // WeaponCore api endpoint

        static Func<MyEntity, int, MyEntity> _getAiFocus;

        public static void Load()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(Channel, HandleMessage);
            MyAPIGateway.Utilities.SendModMessage(Channel, "ApiEndpointRequest");
        }

        public static void Unload()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, HandleMessage);
            _getAiFocus = null;
        }

        static void HandleMessage(object obj)
        {
            var endpoints = obj as IReadOnlyDictionary<string, Delegate>;
            if (endpoints == null) return;

            Delegate del;
            if (!endpoints.TryGetValue("GetAiFocusBase", out del)) return;
            _getAiFocus = del as Func<MyEntity, int, MyEntity>;

            if (_getAiFocus == null)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] unexpected WeaponCore GetAiFocusBase signature: {del?.GetType()}");
            }
        }

        public static bool HasFocusTarget()
        {
            if (_getAiFocus == null) return false; // WeaponCore absent or not ready

            var controller = MyAPIGateway.Session.ControlledObject as IMyShipController;
            var grid = controller?.CubeGrid as MyEntity;
            if (grid == null) return false;

            return _getAiFocus(grid, 0) != null;
        }
    }
}
