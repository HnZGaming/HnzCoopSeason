using HnzCoopSeason.Spawners;
using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.Orks
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
            _encounter.Load(grids);
            _encounter.ForceSpawn(_spawnGroupNames);
        }

        public void Unload(bool sessionUnload)
        {
            _encounter.Unload(sessionUnload);
        }
    }
}