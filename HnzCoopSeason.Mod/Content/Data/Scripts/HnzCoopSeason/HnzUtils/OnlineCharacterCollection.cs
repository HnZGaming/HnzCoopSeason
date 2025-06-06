using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzUtils
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

        public static bool GetAllContainedPlayers(BoundingSphereD sphere, ICollection<IMyPlayer> players)
        {
            var foundPlayers = false;
            foreach (var p in _players)
            {
                if (ContainsPlayer(sphere, p))
                {
                    players.Add(p);
                    foundPlayers = true;
                }
            }

            return foundPlayers;
        }

        public static bool TryGetContainedPlayer(BoundingSphereD sphere, out IMyPlayer player)
        {
            foreach (var p in _players)
            {
                if (ContainsPlayer(sphere, p))
                {
                    player = p;
                    return true;
                }
            }

            player = null;
            return false;
        }

        static bool ContainsPlayer(BoundingSphereD sphere, IMyPlayer player)
        {
            if (player.Character == null) return false;
            return sphere.Contains(player.Character.GetPosition()) == ContainmentType.Contains;
        }

        public static bool ContainsPlayer(BoundingSphereD sphere)
        {
            IMyPlayer _;
            return TryGetContainedPlayer(sphere, out _);
        }
    }
}