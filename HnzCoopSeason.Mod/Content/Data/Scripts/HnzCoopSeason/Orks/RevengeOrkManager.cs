using System;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.Orks
{
    public sealed class RevengeOrkManager
    {
        public static readonly RevengeOrkManager Instance = new RevengeOrkManager();

        RevengeOrk _revengeOrk;
        int _increment;

        public void Load()
        {
        }

        public void Unload()
        {
            _revengeOrk?.Unload(true);
        }

        public void Spawn(Vector3 position, string[] spawnGroupNames)
        {
            _revengeOrk?.Unload(false);
            _revengeOrk = new RevengeOrk($"{_increment++}", position, spawnGroupNames);
            _revengeOrk.Load(Array.Empty<IMyCubeGrid>());
        }
    }
}