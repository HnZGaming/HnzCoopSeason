using System;
using System.Linq;
using HnzCoopSeason.MES;
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
        readonly MesGridGroup _mesGridGroup;
        bool _encounterActive;

        public MesEncounter(string gridId, MesEncounterConfig[] configs, Vector3D position)
        {
            _gridId = gridId;
            _configs = configs;
            _position = position;
            _mesGridGroup = new MesGridGroup(gridId);
        }

        public event Action<IMyCubeGrid> OnMainGridSet
        {
            add { _mesGridGroup.OnMainGridSet += value; }
            remove { _mesGridGroup.OnMainGridSet -= value; }
        }

        public event Action<IMyCubeGrid> OnMainGridUnset
        {
            add { _mesGridGroup.OnMainGridUnset += value; }
            remove { _mesGridGroup.OnMainGridUnset -= value; }
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _mesGridGroup.Load(grids);
        }

        public void Unload(bool sessionUnload)
        {
            _mesGridGroup.Unload(sessionUnload);
        }

        public void SetActive(bool active)
        {
            _encounterActive = active;
        }

        public void Update()
        {
            _mesGridGroup.Update();

            if (MyAPIGateway.Session.GameplayFrameCounter % 60 != 0) return;
            if (!_encounterActive) return;
            if (_mesGridGroup.State != MesGridGroup.SpawningState.Idle) return;

            IMyPlayer player;
            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (!OnlineCharacterCollection.TryGetContainedPlayer(sphere, out player)) return;

            MyLog.Default.Info($"[HnzCoopSeason] encounter {_gridId} player nearby");

            Spawn(CalcConfigIndex(), player.GetPosition());
        }

        public void ForceSpawn(int configIndex)
        {
            Vector3D? knownPlayerPosition = null;
            IMyPlayer player;
            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (OnlineCharacterCollection.TryGetContainedPlayer(sphere, out player))
            {
                knownPlayerPosition = player.GetPosition();
            }

            MyLog.Default.Info($"[HnzCoopSeason] encounter {_gridId} player nearby");
            Spawn(configIndex, knownPlayerPosition);
        }

        void Spawn(int configIndex, Vector3D? playerPosition)
        {
            var config = _configs[configIndex];
            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            var clearance = SessionConfig.Instance.EncounterClearance;
            MatrixD matrix;
            if (!SpawnUtils.TryCalcMatrix(config.SpawnType, sphere, clearance, out matrix))
            {
                MyLog.Default.Error($"[HnzCoopSeason] encounter {_gridId} failed to find a spawnable position: {config}");
                return;
            }

            // rotate so that sidekicks can populate in correct positions
            if (playerPosition.HasValue && config.SpawnType == SpawnType.SpaceShip)
            {
                var up = (playerPosition.Value - matrix.Translation).Normalized();
                var forward = Vector3D.CalculatePerpendicularVector(up);
                matrix = MatrixD.CreateWorld(matrix.Translation, forward, up);
            }

            MyLog.Default.Info($"[HnzCoopSeason] requesting spawn; config index: {configIndex}");
            var spawnGroupNames = config.SpawnGroups.Select(g => g.SpawnGroup).ToArray();
            _mesGridGroup.RequestSpawn(spawnGroupNames, matrix, SessionConfig.Instance.EncounterClearance);
        }

        int CalcConfigIndex()
        {
            if (_configs.Length == 1) return 0;

            var progressLevel = Session.Instance.GetProgressLevel();
            var weights = _configs.Select(c => GetWeight(c, progressLevel)).ToArray();
            if (weights.Length == 0)
            {
                MyLog.Default.Warning($"[HnzCoopSeason] encounter {_gridId} no configs eligible; selecting 0");
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
            return $"MesEncounter({nameof(_gridId)}: {_gridId}, {nameof(_encounterActive)}: {_encounterActive}, {nameof(_mesGridGroup)}: {_mesGridGroup})";
        }
    }
}