﻿using HnzPveSeason.Utils;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class Ork
    {
        readonly string _poiId;
        readonly MesGrid _mesGrid;
        readonly MesStaticEncounter _mesEncounter;

        public Ork(string poiId, MesStaticEncounterConfig[] configs, SpawnEnvironment environment, Vector3D position)
        {
            _poiId = poiId;
            _mesGrid = new MesGrid($"{_poiId}-ork");
            _mesEncounter = new MesStaticEncounter(_mesGrid, environment, position, configs);
        }

        public int EncounterIndex
        {
            get { return _mesEncounter.ConfigIndex; }
            set { _mesEncounter.ConfigIndex = value; }
        }

        public void Load(IMyCubeGrid[] grids)
        {
            _mesGrid.Load();
            _mesGrid.OnGridSet += OnMesGridSet;

            if (_mesGrid.TryRecover(grids))
            {
                MyLog.Default.Info($"[HnzPveSeason] ork {_poiId} recovered");
            }
        }

        public void Unload()
        {
            _mesGrid.Despawn();
            _mesGrid.Unload();
            _mesGrid.OnGridSet -= OnMesGridSet;
        }

        public void SetActiveEncounter(bool active)
        {
            _mesEncounter.ActiveEncounter = active;
        }

        public void Update()
        {
            _mesGrid.Update();
            _mesEncounter.Update();
        }

        void OnMesGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] ork `{_poiId}` grid set");
        }
    }
}