using System;
using System.Collections.Generic;
using System.Linq;
using HnzPveSeason.Utils;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Contracts;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace HnzPveSeason
{
    public sealed class EconomyFaction
    {
        readonly MyFactionTypeDefinition _definition;
        readonly IMyFaction _myFaction;

        public EconomyFaction(MyFactionTypeDefinition definition, IMyFaction myFaction)
        {
            _definition = definition;
            _myFaction = myFaction;
        }

        public string Tag => _myFaction.Tag;

        public bool IsMyBlock(IMyCubeBlock block)
        {
            var ownerId = block.OwnerId;
            var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
            return faction.FactionId == _myFaction.FactionId;
        }

        public void UpdateStoreBlock(IMyStoreBlock storeBlock)
        {
            storeBlock.ClearItems();

            foreach (var itemId in _definition.OffersList)
            {
                MyPhysicalItemDefinition d;
                if (!MyDefinitionManager.Static.TryGetDefinition(itemId, out d)) continue;

                //todo randomize amount
                var item = storeBlock.CreateStoreItem(d.Id, d.MinimumOfferAmount, d.MinimalPricePerUnit, StoreItemTypes.Offer);
                storeBlock.InsertStoreItem(item);

                MyLog.Default.Debug($"[HnzPveSeason] faction {_myFaction.Tag} offer: {d.Id.SubtypeName}");
            }

            foreach (var itemId in _definition.OrdersList)
            {
                MyPhysicalItemDefinition d;
                if (!MyDefinitionManager.Static.TryGetDefinition(itemId, out d)) continue;

                //todo randomize amount
                var item = storeBlock.CreateStoreItem(d.Id, d.MinimumOrderAmount, d.MinimalPricePerUnit, StoreItemTypes.Order);
                storeBlock.InsertStoreItem(item);

                MyLog.Default.Debug($"[HnzPveSeason] faction {_myFaction.Tag} order: {d.Id.SubtypeName}");
            }
        }

        public void UpdateContractBlock(long blockId, HashSet<long> contractIds)
        {
            // remove invalid contracts 
            contractIds.RemoveWhere(c => CanKeepPosted(c));

            var remainingSlotCount = Math.Max(0, _definition.MaxContractCount - contractIds.Count);
            for (var i = 0; i < remainingSlotCount; i++)
            {
                var contract = CreateContract(blockId);
                if (contract == null) continue; // repair contract stuff

                // make sure merchants have money to pay
                MyAPIGateway.Players.RequestChangeBalance(_myFaction.FounderId, contract.MoneyReward + 1);

                // post up the contract
                var result = MyAPIGateway.ContractSystem.AddContract(contract);
                if (!result.Success)
                {
                    //todo log error
                    continue;
                }

                contractIds.Add(result.ContractId);
            }
        }

        IMyContract CreateContract(long blockId)
        {
            var serializedId = new SerializableDefinitionId(_definition.Id.TypeId, _definition.Id.SubtypeName);
            var contractTypes = MyDefinitionManager.Static.GetContractTypeDefinitions().Values.ToArray();
            var weights = new float[contractTypes.Length];
            for (var i = 0; i < contractTypes.Length; i++)
            {
                float chance;
                contractTypes[i].ChancesPerFactionType.TryGetValue(serializedId, out chance);
                weights[i] = chance;
            }

            var contractTypeIndex = MathUtils.WeightedRandom(weights);
            var contract = CreateContract(contractTypes[contractTypeIndex], blockId);
            if (contract == null)
            {
                MyLog.Default.Warning($"[HnzPveSeason] unsupported contract type: {contractTypeIndex}");
            }

            return contract;
        }

        IMyContract CreateContract(MyContractTypeDefinition definition, long blockId)
        {
            switch (definition.Id.SubtypeName)
            {
                case "Deliver": return CreateDeliverContract((MyContractTypeDeliverDefinition)definition, blockId);
                case "ObtainAndDeliver": return CreateAcquisitionContract((MyContractTypeObtainAndDeliverDefinition)definition, blockId);
                case "Escort": return CreateEscortContract((MyContractTypeEscortDefinition)definition, blockId);
                case "Find": return CreateFindContract((MyContractTypeFindDefinition)definition, blockId);
                case "Hunt": return CreateHuntContract((MyContractTypeHuntDefinition)definition, blockId);
                case "Repair": return null;
                default: throw new InvalidOperationException("unknown contract type: " + definition.Id.SubtypeName);
            }
        }

        IMyContract CreateDeliverContract(MyContractTypeDeliverDefinition d, long startBlockId)
        {
            MyLog.Default.Info($"[HnzPveSeason] create deliver contract: {startBlockId}");
            return null;
        }

        IMyContract CreateAcquisitionContract(MyContractTypeObtainAndDeliverDefinition d, long blockId)
        {
            MyLog.Default.Info($"[HnzPveSeason] create acquisition contract: {blockId}");
            return null;
        }

        IMyContract CreateEscortContract(MyContractTypeEscortDefinition d, long blockId)
        {
            MyLog.Default.Info($"[HnzPveSeason] create escort contract: {blockId}");
            return null;
        }

        IMyContract CreateFindContract(MyContractTypeFindDefinition d, long blockId)
        {
            MyLog.Default.Info($"[HnzPveSeason] create find contract: {blockId}");
            return null;
        }

        IMyContract CreateHuntContract(MyContractTypeHuntDefinition d, long blockId)
        {
            MyLog.Default.Info($"[HnzPveSeason] create hunt contract: {blockId}");
            return null;
        }

        static bool CanKeepPosted(long contractId)
        {
            var contractState = MyAPIGateway.ContractSystem.GetContractState(contractId);
            return contractState == MyCustomContractStateEnum.Active ||
                   contractState == MyCustomContractStateEnum.Inactive;
        }
    }
}