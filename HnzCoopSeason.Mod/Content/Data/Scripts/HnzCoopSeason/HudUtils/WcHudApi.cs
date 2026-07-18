using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace HnzCoopSeason.HudUtils
{
    /// <summary>
    ///     Minimal WeaponCore mod-API client; only pulls the focus-target accessor.
    ///     Used to hide our screen-top meter while WeaponCore's target HUD is showing.
    /// </summary>
    public static class WcHudApi
    {
        const long Channel = 67549756549; // WeaponCore api endpoint channel

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
            if (endpoints.TryGetValue("GetAiFocusBase", out del))
            {
                _getAiFocus = (Func<MyEntity, int, MyEntity>)del;
            }
        }

        /// <summary>
        ///     True while the player's controlled grid has a WeaponCore focus target,
        ///     i.e. while WeaponCore is drawing its target info panel.
        /// </summary>
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
