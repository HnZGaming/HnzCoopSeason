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
        readonly string _factionTag;
        bool _encounterActive;

        public MesStaticEncounter(string gridId, string prefix, MesStaticEncounterConfig[] configs, Vector3D position, string factionTag, bool ignoreForDespawn)
        {
            _gridId = gridId;
            _configs = configs;
            _factionTag = factionTag;
            _position = position;
            _mesGrid = new MesGrid(gridId, prefix, ignoreForDespawn);
            ConfigIndex = CalcConfigIndex();
        }

        public int ConfigIndex { get; set; }

        public MesStaticEncounterConfig Config => _configs[Math.Min(ConfigIndex, _configs.Length - 1)];

        public event Action<IMyCubeGrid> OnGridSet
        {
            add { _mesGrid.OnGridSet += value; }
            remove { _mesGrid.OnGridSet -= value; }
        }

        public event Action<IMyCubeGrid> OnGridUnset
        {
            add { _mesGrid.OnGridUnset += value; }
            remove { _mesGrid.OnGridUnset -= value; }
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _mesGrid.Load();

            if (_mesGrid.TryRecover(grids))
            {
                MyLog.Default.Info($"[HnzPveSeason] encounter {_gridId} recovered grid from save");
            }
        }

        public void Unload(bool sessionUnload)
        {
            if (!sessionUnload) // otherwise fails to unload session
            {
                _mesGrid.Despawn();
            }

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
            IMyPlayer player;
            if (!OnlineCharacterCollection.TryGetContainedPlayer(sphere, out player)) return;

            MyLog.Default.Info($"[HnzPveSeason] encounter `{_mesGrid.Id}` player nearby: '{player.DisplayName}'");

            var matrix = TryCalcMatrix();
            if (matrix == null)
            {
                MyLog.Default.Error($"[HnzPveSeason] encounter `{_mesGrid.Id}` failed to find a spawnable position: {Config}");
                return;
            }

            _mesGrid.RequestSpawn(Config.SpawnGroup, Config.MainPrefab, _factionTag, matrix.Value);

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