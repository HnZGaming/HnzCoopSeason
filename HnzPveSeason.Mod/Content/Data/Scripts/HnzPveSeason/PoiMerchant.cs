using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class PoiMerchant : IPoiObserver
    {
        readonly string _poiId;
        readonly MesStaticEncounter _encounter;

        public PoiMerchant(string poiId, Vector3D position, MesStaticEncounterConfig[] configs)
        {
            _poiId = poiId;
            _encounter = new MesStaticEncounter($"{poiId}-merchant", configs, position);
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnSpawned += OnGridSpawned;
            _encounter.OnDespawned += OnGridDespawned;

            _encounter.Load(grids);
        }

        void IPoiObserver.Unload()
        {
            _encounter.Unload();

            _encounter.OnSpawned -= OnGridSpawned;
            _encounter.OnDespawned -= OnGridDespawned;
        }

        void IPoiObserver.Update()
        {
            _encounter.Update();
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _encounter.SetActive(state == PoiState.Released);
        }

        void OnGridSpawned(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} merchant spawn");

            var contractBlocks = new List<IMySlimBlock>();
            grid.GetBlocks(contractBlocks, b => IsContractBlock(b));
            if (contractBlocks.Count == 0)
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} contract block not found");
                return;
            }

            if (contractBlocks.Count >= 2)
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} contract blocks multiple found");
                return;
            }
        }

        void OnGridDespawned(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} merchant despawn");
        }

        static bool IsContractBlock(IMySlimBlock slimBlock)
        {
            if (slimBlock.FatBlock == null) return false;
            var blockSubtype = slimBlock.FatBlock.BlockDefinition.SubtypeId;
            return blockSubtype.IndexOf("ContractBlock", StringComparison.Ordinal) > -1;
        }
    }
}