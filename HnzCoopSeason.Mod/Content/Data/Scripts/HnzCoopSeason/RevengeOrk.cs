using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class RevengeOrk
    {
        readonly MesEncounter _encounter;
        readonly string[] _spawnGroupNames;

        public RevengeOrk(string id, Vector3D position, string[] spawnGroupNames)
        {
            _spawnGroupNames = spawnGroupNames;
            _encounter = new MesEncounter($"revenge-ork-{id}", position);
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _encounter.SpawnDelegate = EncounterSpawnDelegate;
            _encounter.Load(grids);
            _encounter.SetActive(true);
        }

        public void Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
            _encounter.SpawnDelegate = null;
        }

        public void Update()
        {
            _encounter.Update();
        }

        bool EncounterSpawnDelegate(int playerCount, List<string> spawnGroupNames)
        {
            spawnGroupNames.AddRange(_spawnGroupNames);
            return true;
        }
    }
}