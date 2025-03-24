using System;
using System.Collections.Generic;
using System.Linq;
using HnzCoopSeason.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason
{
    public sealed class MesEncounter
    {
        public delegate bool SpawnDelegateType(int playerCount, List<string> spawnGroupNames);

        readonly string _gridId;
        readonly Vector3D _position;
        readonly MesGridGroup _mesGridGroup;
        bool _encounterActive;

        public MesEncounter(string gridId, Vector3D position)
        {
            _gridId = gridId;
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

        public SpawnDelegateType SpawnDelegate { get; set; }

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

            var players = new List<IMyPlayer>();
            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (!OnlineCharacterCollection.GetAllContainedPlayers(sphere, players)) return;

            var spawnGroupNames = new List<string>();
            if (!SpawnDelegate(players.Count, spawnGroupNames)) return;

            MyLog.Default.Info($"[HnzCoopSeason] encounter {_gridId} player nearby; players: {players.Select(p => p.DisplayName).ToStringSeq()}");

            var playerPosition = players
                .Select(p => p.GetPosition())
                .OrderBy(p => Vector3D.Distance(p, _position))
                .First();

            Spawn(spawnGroupNames, playerPosition);
        }

        public void ForceSpawn(IReadOnlyList<string> spawnGroupNames)
        {
            Vector3D? knownPlayerPosition = null;
            IMyPlayer player;
            var sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius);
            if (OnlineCharacterCollection.TryGetContainedPlayer(sphere, out player))
            {
                knownPlayerPosition = player.GetPosition();
            }

            MyLog.Default.Info($"[HnzCoopSeason] encounter {_gridId} force spawn");
            Spawn(spawnGroupNames, knownPlayerPosition);
        }

        void Spawn(IReadOnlyList<string> spawnGroupNames, Vector3D? playerPosition)
        {
            var matrixGenerator = new SpawnMatrixBuilder
            {
                Sphere = new BoundingSphereD(_position, SessionConfig.Instance.EncounterRadius),
                Clearance = SessionConfig.Instance.EncounterClearance,
                SnapToVoxel = false,
                Count = spawnGroupNames.Count,
                PlayerPosition = playerPosition,
            };

            if (!matrixGenerator.TryBuild())
            {
                MyLog.Default.Error($"[HnzCoopSeason] encounter {_gridId} failed to find a spawnable position; groups: {spawnGroupNames.ToStringSeq()}");
                return;
            }

            _mesGridGroup.RequestSpawn(spawnGroupNames, matrixGenerator);
        }

        public override string ToString()
        {
            return $"MesEncounter({nameof(_gridId)}: {_gridId}, {nameof(_encounterActive)}: {_encounterActive}, {nameof(_mesGridGroup)}: {_mesGridGroup})";
        }
    }
}