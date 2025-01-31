using System;
using System.Linq;
using HnzPveSeason.Utils;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class MesStaticEncounter
    {
        readonly MesGrid _mesGrid;
        readonly SpawnEnvironment _environment;
        readonly Vector3 _position;
        readonly MesStaticEncounterConfig[] _configs;

        public MesStaticEncounter(MesGrid mesGrid, SpawnEnvironment environment, Vector3 position, MesStaticEncounterConfig[] configs)
        {
            _mesGrid = mesGrid;
            _environment = environment;
            _position = position;
            _configs = configs;
            ConfigIndex = CalcConfigIndex();
        }

        public bool ActiveEncounter { get; set; }
        public int ConfigIndex { get; set; }

        int CalcConfigIndex()
        {
            if (_configs.Length == 1)
            {
                return 0;
            }

            var weights = _configs.Select(c => c.Weight).ToArray();
            return MathUtils.WeightedRandom(weights);
        }

        public void Update()
        {
            if (!ActiveEncounter) return;
            if (_mesGrid.State != MesGrid.SpawningState.Idle) return;
            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;

            var config = _configs[Math.Min(ConfigIndex, _configs.Length - 1)];
            var sphere = new BoundingSphereD(_position, config.SpawnRadius);
            if (!OnlineCharacterCollection.ContainsCharacter(sphere)) return;

            var matrix = SpawnUtils.TryCalcMatrix(_environment, sphere, config.ClearanceRadius);
            if (matrix == null)
            {
                MyLog.Default.Error($"[HnzPveSeason] MesGridEncounter `{_mesGrid.Id}` failed to find spawning matrix");
                return;
            }

            _mesGrid.RequestSpawn(config.SpawnGroup, matrix.Value);

            ConfigIndex = CalcConfigIndex();
        }
    }
}