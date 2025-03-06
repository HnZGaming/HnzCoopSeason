using System.Collections.Generic;
using System.Linq;
using HnzPveSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public static class OnlineCharacterCollection
    {
        static readonly List<IMyPlayer> _players = new List<IMyPlayer>();

        public static void Unload()
        {
            _players.Clear();
        }

        public static void Update()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 == 0)
            {
                _players.Clear();
                MyAPIGateway.Players.GetPlayers(_players);
            }
        }

        public static bool TryGetContainedPlayer(BoundingSphereD sphere, out IMyPlayer player)
        {
            foreach (var p in _players)
            {
                if (p.Character == null) continue;
                var position = p.GetPosition();
                if (sphere.Contains(position) == ContainmentType.Contains)
                {
                    player = p;
                    return true;
                }
            }

            player = null;
            return false;
        }

        public static bool ContainsPlayer(BoundingSphereD sphere)
        {
            IMyPlayer _;
            return TryGetContainedPlayer(sphere, out _);
        }
    }
}