using System;
using System.Collections.Generic;
using System.Linq;
using HnzPveSeason.Utils;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Contracts;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace HnzPveSeason
{
    public sealed class EconomyFaction
    {
        readonly MyFactionTypeDefinition _factionType;
        readonly IMyFaction _faction;

        public EconomyFaction(MyFactionTypeDefinition factionType, IMyFaction faction)
        {
            _factionType = factionType;
            _faction = faction;
        }

        public string Tag => _faction.Tag;

        public bool IsMyBlock(IMyCubeBlock block)
        {
            var ownerId = block.OwnerId;
            var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(ownerId);
            return faction.FactionId == _faction.FactionId;
        }

        public void UpdateStoreItems(IMyStoreBlock storeBlock)
        {
            storeBlock.ClearItems();

            foreach (var itemId in _factionType.OffersList)
            {
                MyPhysicalItemDefinition d;
                if (!MyDefinitionManager.Static.TryGetDefinition(itemId, out d)) continue;

                if (!d.CanPlayerOrder) continue;

                //todo progressive amount
                var amount = (int)MathHelper.Lerp(d.MinimumOfferAmount, d.MaximumOfferAmount, MyRandom.Instance.NextDouble());

                //todo progressive price
                var pricePerUnit = 0;
                CalculateItemMinimalPrice(d.Id, ref pricePerUnit);

                if (pricePerUnit <= 0)
                {
                    MyLog.Default.Error($"[HnzPveSeason] invalid price: {pricePerUnit} for item {d.Id}");
                    continue;
                }

                var item = storeBlock.CreateStoreItem(d.Id, amount, pricePerUnit, StoreItemTypes.Offer);
                storeBlock.InsertStoreItem(item);

                MyLog.Default.Debug($"[HnzPveSeason] faction {_faction.Tag} offer: {d.Id.SubtypeName}, {d.MinimalPricePerUnit}, {_factionType.OfferPriceStartingMultiplier}");
            }

            foreach (var itemId in _factionType.OrdersList)
            {
                MyPhysicalItemDefinition d;
                if (!MyDefinitionManager.Static.TryGetDefinition(itemId, out d)) continue;

                //todo randomize amount
                var item = storeBlock.CreateStoreItem(d.Id, d.MinimumOrderAmount, d.MinimalPricePerUnit, StoreItemTypes.Order);
                storeBlock.InsertStoreItem(item);

                MyLog.Default.Debug($"[HnzPveSeason] faction {_faction.Tag} order: {d.Id.SubtypeName}");
            }
        }

        public void UpdateContracts(long blockId, HashSet<long> contractIds)
        {
            // remove invalid contracts 
            contractIds.RemoveWhere(c => CanKeepPosted(c));

            var newContractCount = _factionType.MaxContractCount - contractIds.Count;
            for (var i = 0; i < newContractCount; i++)
            {
                var contractType = GetRandomContractType();
                var contract = CreateContract(contractType, blockId);
                if (contract == null)
                {
                    MyLog.Default.Warning($"[HnzPveSeason] contract not implemented: {contractType}");
                    continue;
                }

                // make sure merchants have money to pay
                MyAPIGateway.Players.RequestChangeBalance(_faction.FounderId, contract.MoneyReward + 1);

                // post up the contract
                var result = MyAPIGateway.ContractSystem.AddContract(contract);
                if (!result.Success)
                {
                    MyLog.Default.Error($"[HnzPveSeason] failed to add contract; faction: {_faction.Tag}, block ID: {blockId}, type: {contract.GetType()}");
                    continue;
                }

                contractIds.Add(result.ContractId);
            }
        }

        MyContractTypeDefinition GetRandomContractType()
        {
            var serializedId = new SerializableDefinitionId(_factionType.Id.TypeId, _factionType.Id.SubtypeName);
            var contractTypes = MyDefinitionManager.Static.GetContractTypeDefinitions().Values.ToArray();
            var weights = new float[contractTypes.Length];
            for (var i = 0; i < contractTypes.Length; i++)
            {
                float chance;
                contractTypes[i].ChancesPerFactionType.TryGetValue(serializedId, out chance);
                weights[i] = chance;
            }

            var contractTypeIndex = MathUtils.WeightedRandom(weights);
            return contractTypes[contractTypeIndex];
        }

        IMyContract CreateContract(MyContractTypeDefinition contractType, long blockId)
        {
            switch (contractType.Id.SubtypeName)
            {
                case "Deliver": return CreateDeliverContract((MyContractTypeDeliverDefinition)contractType, blockId);
                case "ObtainAndDeliver": return CreateAcquisitionContract((MyContractTypeObtainAndDeliverDefinition)contractType, blockId);
                case "Escort": return CreateEscortContract((MyContractTypeEscortDefinition)contractType, blockId);
                case "Find": return CreateFindContract((MyContractTypeFindDefinition)contractType, blockId);
                case "Hunt": return CreateHuntContract((MyContractTypeHuntDefinition)contractType, blockId);
                case "Repair": return null;
                default: return null; // includes MES stuff
            }
        }

        IMyContract CreateDeliverContract(MyContractTypeDeliverDefinition d, long startBlockId)
        {
            MyLog.Default.Info($"[HnzPveSeason] create deliver contract: {startBlockId}");
            return null;
        }

        IMyContract CreateAcquisitionContract(MyContractTypeObtainAndDeliverDefinition contractType, long blockId)
        {
            MyLog.Default.Info($"[HnzPveSeason] create acquisition contract: {blockId}");

            var items = GetItems(contractType.AvailableItems).ToArray();
            var itemIndex = MyRandom.Instance.Next(items.Length - 1);
            var item = items[itemIndex];
            var amount = (int)MathHelper.Lerp(item.MinimumAcquisitionAmount, item.MaximumAcquisitionAmount, MyRandom.Instance.NextDouble());
            var reward = (int)contractType.MinimumMoney;
            return new MyContractAcquisition(blockId, reward, 0, 0, blockId, item.Id, amount);
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

        static IEnumerable<MyPhysicalItemDefinition> GetItems(IEnumerable<SerializableDefinitionId> itemIdList)
        {
            foreach (var itemId in itemIdList)
            {
                MyPhysicalItemDefinition item;
                if (MyDefinitionManager.Static.TryGetDefinition(itemId, out item))
                {
                    yield return item;
                }
            }
        }

        // copied from vanilla private code
        static void CalculateItemMinimalPrice(MyDefinitionId itemId, ref int minimalPrice)
        {
            MyPhysicalItemDefinition myPhysicalItemDefinition;
            if (MyDefinitionManager.Static.TryGetDefinition(itemId, out myPhysicalItemDefinition) && myPhysicalItemDefinition.MinimalPricePerUnit > 0)
            {
                minimalPrice += myPhysicalItemDefinition.MinimalPricePerUnit;
                return;
            }

            MyBlueprintDefinitionBase myBlueprintDefinitionBase;
            if (!MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(itemId, out myBlueprintDefinitionBase))
            {
                return;
            }

            var num = myPhysicalItemDefinition.IsIngot ? 1f : MyAPIGateway.Session.AssemblerEfficiencyMultiplier;
            var num2 = 0;
            foreach (var item in myBlueprintDefinitionBase.Prerequisites)
            {
                var num3 = 0;
                CalculateItemMinimalPrice(item.Id, ref num3);
                var num4 = (float)item.Amount / num;
                num2 += (int)(num3 * num4);
            }

            var num5 = myPhysicalItemDefinition.IsIngot ? MyAPIGateway.Session.RefinerySpeedMultiplier : MyAPIGateway.Session.AssemblerSpeedMultiplier;
            foreach (var item2 in myBlueprintDefinitionBase.Results)
            {
                if (item2.Id == itemId)
                {
                    var num6 = (float)item2.Amount;
                    if (num6 != 0f)
                    {
                        var num7 = 1f + (float)Math.Log(myBlueprintDefinitionBase.BaseProductionTimeInSeconds + 1f) / num5;
                        minimalPrice += (int)(num2 * (1f / num6) * num7);
                        return;
                    }
                }
            }
        }
    }
}