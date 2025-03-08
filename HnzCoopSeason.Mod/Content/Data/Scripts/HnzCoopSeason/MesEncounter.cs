using System;
using System.Linq;
using HnzCoopSeason.Utils;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class MesEncounter
    {
        readonly string _gridId;
        readonly MesEncounterConfig[] _configs;
        readonly Vector3D _position;
        readonly MesGrid _mesGrid;
        readonly string _factionTag;
        bool _encounterActive;

        public MesEncounter(string gridId, string prefix, MesEncounterConfig[] configs, Vector3D position, string factionTag)
        {
            _gridId = gridId;
            _configs = configs;
            _factionTag = factionTag;
            _position = position;
            _mesGrid = new MesGrid(gridId, prefix);
        }

        public event Action<IMyCubeGrid> OnMainGridSet
        {
            add { _mesGrid.OnMainGridSet += value; }
            remove { _mesGrid.OnMainGridSet -= value; }
        }

        public event Action<IMyCubeGrid> OnMainGridUnset
        {
            add { _mesGrid.OnMainGridUnset += value; }
            remove { _mesGrid.OnMainGridUnset -= value; }
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _mesGrid.Load(grids);
        }

        public void Unload(bool sessionUnload)
        {
            _mesGrid.Unload(sessionUnload);
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
            
            MyVisualScriptLogicProvider.AddGPS("center", "", matrix.Translation, Color.Blue);

            MyLog.Default.Info($"[HnzCoopSeason] requesting spawn; config index: {configIndex}");
            var spawnGroupNames = config.SpawnGroups.Select(g => g.SpawnGroup).ToArray();
            _mesGrid.RequestSpawn(spawnGroupNames, _factionTag, matrix);
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

        static float GetWeight(MesEncounterConfig config, int progressLevel)
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