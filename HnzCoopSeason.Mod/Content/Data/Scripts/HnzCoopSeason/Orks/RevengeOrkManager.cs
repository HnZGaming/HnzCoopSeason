using System;
using System.Collections.Generic;
using HnzCoopSeason.Spawners;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.Orks
{
    public sealed class RevengeOrkManager
    {
        public static readonly RevengeOrkManager Instance = new RevengeOrkManager();

        LinkedList<MesEncounter> _orks;
        int _increment;

        public void Load()
        {
            _orks = new LinkedList<MesEncounter>();
        }

        void Clear(bool sessionUnload)
        {
            foreach (var ork in _orks)
            {
                ork.Unload(sessionUnload);
            }

            _orks.Clear();
        }

        public void Unload()
        {
            Clear(true);
        }

        public void Spawn(Vector3 position, string[] spawnGroupNames)
        {
            var ork = new MesEncounter($"revenge-ork-{_increment++}", position);
            _orks.AddLast(ork);

            ork.Load(Array.Empty<IMyCubeGrid>());
            ork.ForceSpawn(spawnGroupNames);
        }

        public void DespawnAll()
        {
            Clear(false);
        }
    }
}