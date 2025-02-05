using System;
using System.Collections.Generic;
using HnzPveSeason.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Contracts;
using VRage.Game.ModAPI;
using VRage.Serialization;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class PoiMerchant : IPoiObserver
    {
        readonly string _poiId;
        readonly MesStaticEncounter _encounter;
        readonly string _variableKey;
        IMyContract _contract;
        long _contractId;
        long _safeZoneId;

        public PoiMerchant(string poiId, Vector3D position, MesStaticEncounterConfig[] configs)
        {
            _poiId = poiId;
            _encounter = new MesStaticEncounter($"{poiId}-merchant", "[MERCHANTS]", configs, position, true);
            _variableKey = $"HnzPveSeason.PoiMerchant.{_poiId}";
        }

        void IPoiObserver.Load(IMyCubeGrid[] grids)
        {
            _encounter.OnGridSet += OnGridSet;
            _encounter.OnGridUnset += OnGridUnset;

            LoadFromSandbox();

            _encounter.Load(grids);
        }

        void IPoiObserver.Unload()
        {
            _encounter.Unload();

            _encounter.OnGridSet -= OnGridSet;
            _encounter.OnGridUnset -= OnGridUnset;
        }

        void IPoiObserver.Update()
        {
            _encounter.Update();
        }

        void IPoiObserver.OnStateChanged(PoiState state)
        {
            _encounter.SetActive(state == PoiState.Released);

            if (state == PoiState.Occupied)
            {
                _encounter.Despawn();
            }
        }

        void OnGridSet(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} merchant grid set");

            SetUpContracts(grid);
            SetUpSafezone(grid);
            SaveToSandbox();
        }

        void OnGridUnset(IMyCubeGrid grid)
        {
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} merchant grid unset");

            DisposeContracts();
            RemoveSafezone();
            SaveToSandbox();
        }

        void SetUpContracts(IMyCubeGrid grid)
        {
            // find contract blocks
            var blocks = new List<IMySlimBlock>();
            grid.GetBlocks(blocks, b => IsContractBlock(b));
            if (blocks.Count == 0)
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} contract block not found");
                return;
            }

            // can't have multiple contract blocks
            if (blocks.Count >= 2)
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} contract blocks multiple found");
                return;
            }

            var block = blocks[0];
            var blockId = block.FatBlock.EntityId;
            var contractState = MyAPIGateway.ContractSystem.GetContractState(_contractId);
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} contract block found: {blockId}, {_contractId}, {contractState}");

            // keep the existing contracts posted up
            if (contractState == MyCustomContractStateEnum.Active || contractState == MyCustomContractStateEnum.Inactive)
            {
                MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} contract already exists");
                return;
            }

            // choose contracts to post up
            var config = SessionConfig.Instance.Contracts[0]; //todo
            _contract = new MyContractAcquisition(blockId, config.Reward, config.Collateral, config.Duration, blockId, config.ItemDefinitionId, config.ItemAmount);

            // grid must be owned by a faction
            IMyFaction faction;
            if (!VRageUtils.TryGetFaction(blockId, out faction))
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} contract faction not found; block id: {blockId}");
                return;
            }

            // grid must be owned by an NPC faction
            var steamId = MyAPIGateway.Players.TryGetSteamId(faction.FounderId);
            if (steamId != 0)
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} contract block not owned by NPC");
                return;
            }

            // make sure merchants have money to pay
            MyAPIGateway.Players.RequestChangeBalance(faction.FounderId, _contract.MoneyReward + 1);

            // post up the contract
            var result = MyAPIGateway.ContractSystem.AddContract(_contract);
            if (!result.Success)
            {
                MyLog.Default.Error($"[HnzPveSeason] POI {_poiId} contract failed adding to system");
                return;
            }

            _contractId = result.ContractId;
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} contract added; id: {_contractId}");
        }

        void DisposeContracts()
        {
            if (_contractId != 0)
            {
                MyAPIGateway.ContractSystem?.RemoveContract(_contractId);
                _contractId = 0;
            }
        }

        void SetUpSafezone(IMyCubeGrid grid)
        {
            MySafeZone safezone;
            if (TryGetExistingSafeZone(out safezone))
            {
                MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} safezone already exists");
                return;
            }

            safezone = (MySafeZone)MySessionComponentSafeZones.CrateSafeZone(
                grid.WorldMatrix,
                MySafeZoneShape.Sphere,
                MySafeZoneAccess.Blacklist,
                null, null, 50f, true, true,
                name: $"poi-{_poiId}");

            MySessionComponentSafeZones.AddSafeZone(safezone);
            _safeZoneId = safezone.EntityId;
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} safezone created");
        }

        void RemoveSafezone()
        {
            MySafeZone safezone;
            if (!TryGetExistingSafeZone(out safezone))
            {
                MyLog.Default.Warning($"[HnzPveSeason] POI {_poiId} safezone not found");
                return;
            }

            MySessionComponentSafeZones.RemoveSafeZone(safezone);
            safezone.Close();
            MyEntities.Remove(safezone);

            _safeZoneId = 0;
            MyLog.Default.Info($"[HnzPveSeason] POI {_poiId} safezone removed");
        }

        bool TryGetExistingSafeZone(out MySafeZone safezone)
        {
            safezone = MyAPIGateway.Entities.GetEntityById(_safeZoneId) as MySafeZone;
            return safezone != null;
        }

        void SaveToSandbox()
        {
            var data = new SerializableDictionary<string, object>
            {
                [nameof(_contractId)] = _contractId,
                [nameof(_safeZoneId)] = _safeZoneId,
            };
            MyAPIGateway.Utilities.SetVariable(_variableKey, data);
        }

        void LoadFromSandbox()
        {
            SerializableDictionary<string, object> data;
            if (MyAPIGateway.Utilities.GetVariable(_variableKey, out data))
            {
                var dictionary = data.Dictionary;
                _contractId = dictionary.GetValueOrDefault(nameof(_contractId), (long)0);
                _safeZoneId = dictionary.GetValueOrDefault(nameof(_safeZoneId), (long)0);
            }
        }

        static bool IsContractBlock(IMySlimBlock slimBlock)
        {
            if (slimBlock.FatBlock == null) return false;
            var blockSubtype = slimBlock.FatBlock.BlockDefinition.SubtypeId;
            return blockSubtype.IndexOf("ContractBlock", StringComparison.Ordinal) > -1;
        }
    }
}