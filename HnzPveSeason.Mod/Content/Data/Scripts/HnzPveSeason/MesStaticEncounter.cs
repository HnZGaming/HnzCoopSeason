using System;
using System.Linq;
using HnzPveSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class MesStaticEncounter
    {
        readonly string _gridId;
        readonly MesStaticEncounterConfig[] _configs;
        readonly Vector3D _position;
        readonly MesGrid _mesGrid;
        bool _encounterActive;

        public MesStaticEncounter(string gridId, MesStaticEncounterConfig[] configs, Vector3D position)
        {
            _gridId = gridId;
            _configs = configs;
            _position = position;
            _mesGrid = new MesGrid(gridId);
            ConfigIndex = CalcConfigIndex();
        }

        public int ConfigIndex { get; set; }

        MesStaticEncounterConfig Config => _configs[Math.Min(ConfigIndex, _configs.Length - 1)];

        public event Action<IMyCubeGrid> OnSpawned
        {
            add { _mesGrid.OnGridSet += value; }
            remove { _mesGrid.OnGridSet -= value; }
        }

        public event Action<IMyCubeGrid> OnDespawned
        {
            add { _mesGrid.OnGridUnset += value; }
            remove { _mesGrid.OnGridUnset -= value; }
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _mesGrid.Load();

            if (_mesGrid.TryRecover(grids))
            {
                MyLog.Default.Info($"[HnzPveSeason] {_gridId} recovered grid from save");
            }
        }

        public void Unload()
        {
            _mesGrid.Despawn();
            _mesGrid.Unload();
        }

        public void SetActive(bool active)
        {
            _encounterActive = active;
        }

        public void Update()
        {
            _mesGrid.Update();

            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;
            if (!_encounterActive) return;
            if (_mesGrid.State != MesGrid.SpawningState.Idle) return;

            var sphere = new BoundingSphereD(_position, Config.Area);
            if (!OnlineCharacterCollection.ContainsCharacter(sphere)) return;

            MyLog.Default.Info($"[HnzPveSeason] poi encounter `{_mesGrid.Id}` found a character nearby");

            var matrix = TryCalcMatrix();
            if (matrix == null)
            {
                MyLog.Default.Error($"[HnzPveSeason] poi encounter `{_mesGrid.Id}` failed to find a spawnable position: {Config}");
                return;
            }

            _mesGrid.RequestSpawn(Config.SpawnGroup, matrix.Value);

            ConfigIndex = CalcConfigIndex();
        }

        public void Despawn()
        {
            _mesGrid.Despawn();
        }

        int CalcConfigIndex()
        {
            if (_configs.Length == 1) return 0;

            var weights = _configs.Select(c => c.Weight).ToArray();
            return MathUtils.WeightedRandom(weights);
        }

        MatrixD? TryCalcMatrix()
        {
            var sphere = new BoundingSphereD(_position, Config.Area);
            var clearance = Config.Clearance;

            if (Config.Planetary)
            {
                return Config.SnapToVoxel
                    ? SpawnUtils.TryCalcSurfaceMatrix(sphere, clearance)
                    : SpawnUtils.TryCalcOrbitMatrix(sphere, clearance);
            }

            return SpawnUtils.TryCalcSpaceMatrix(sphere, clearance);
        }
    }
}