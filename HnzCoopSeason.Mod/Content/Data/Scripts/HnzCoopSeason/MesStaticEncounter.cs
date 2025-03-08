using System;
using System.Linq;
using HnzCoopSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
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
        }

        public event Action<IMyCubeGrid, bool> OnGridSet
        {
            add { _mesGrid.OnGridSet += value; }
            remove { _mesGrid.OnGridSet -= value; }
        }

        public event Action<IMyCubeGrid> OnGridUnset
        {
            add { _mesGrid.OnGridUnset += value; }
            remove { _mesGrid.OnGridUnset -= value; }
        }

        public void Load(IMyCubeGrid[] grids, bool recover, bool clear)
        {
            _mesGrid.Load();

            if (recover && _mesGrid.TryRecover(grids))
            {
                MyLog.Default.Info($"[HnzCoopSeason] encounter {_gridId} recovered grid from save");
            }

            if (clear)
            {
                _mesGrid.CloseAllMyGrids(grids);
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

            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (!OnlineCharacterCollection.ContainsPlayer(sphere)) return;

            MyLog.Default.Info($"[HnzCoopSeason] encounter {_mesGrid.Id} player nearby");

            Spawn(CalcConfigIndex());
        }

        public void Spawn(int configIndex)
        {
            var config = _configs[configIndex];
            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            var clearance = SessionConfig.Instance.EncounterClearance;
            MatrixD matrix;
            if (!SpawnUtils.TryCalcMatrix(config.SpawnType, sphere, clearance, out matrix))
            {
                MyLog.Default.Error($"[HnzCoopSeason] encounter {_mesGrid.Id} failed to find a spawnable position: {config}");
                return;
            }

            MyLog.Default.Info($"[HnzCoopSeason] requesting spawn; config index: {configIndex}");
            _mesGrid.RequestSpawn(config.SpawnGroup, config.MainPrefab, _factionTag, matrix);
        }

        int CalcConfigIndex()
        {
            if (_configs.Length == 1) return 0;

            var progressLevel = Session.Instance.GetProgressLevel();
            var weights = _configs.Select(c => GetWeight(c, progressLevel)).ToArray();
            if (weights.Length == 0)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] encounter {_mesGrid.Id} no configs eligible; selecting 0");
                return 0;
            }

            return MathUtils.WeightedRandom(weights);
        }

        static float GetWeight(MesStaticEncounterConfig config, int progressLevel)
        {
            if (progressLevel != config.ProgressLevel) return 0;
            return config.Weight;
        }

        public override string ToString()
        {
            return $"{nameof(_gridId)}: {_gridId}, {nameof(_factionTag)}: {_factionTag}, {nameof(_encounterActive)}: {_encounterActive}";
        }
    }
}