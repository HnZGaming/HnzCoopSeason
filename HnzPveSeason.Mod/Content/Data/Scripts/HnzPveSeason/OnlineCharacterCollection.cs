using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
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
            _players.Clear();
            MyAPIGateway.Players.GetPlayers(_players);
        }

        public static bool ContainsCharacter(BoundingSphereD sphere)
        {
            foreach (var player in _players)
            {
                if (player.Character == null) continue;
                var position = player.GetPosition();
                if (sphere.Contains(position) == ContainmentType.Contains)
                {
                    return true;
                }
            }

            return false;
        }
    }
}