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
            var allExistingItems = new List<IMyStoreItem>();
            storeBlock.GetStoreItems(allExistingItems);

            var existingOffers = new Dictionary<MyDefinitionId, int>();
            var existingOrders = new Dictionary<MyDefinitionId, int>();
            foreach (var item in allExistingItems)
            {
                if (item.Item == null) continue; // shouldn't happen

                var id = item.Item.Value.ToDefinitionId();
                var dic = item.StoreItemType == StoreItemTypes.Offer ? existingOffers : existingOrders;
                dic[id] = item.Amount;
            }

            storeBlock.ClearItems();

            foreach (var itemId in _factionType.OffersList)
            {
                MyPhysicalItemDefinition d;
                if (!MyDefinitionManager.Static.TryGetDefinition(itemId, out d)) continue;
                if (!d.CanPlayerOrder) continue;

                const int FillCount = 10;
                var id = d.Id;
                var existingAmount = existingOffers.GetValueOrDefault(id, 0);
                var fillAmount = (int)(MathHelper.Lerp(d.MinimumOfferAmount, d.MaximumOfferAmount, MyRandom.Instance.NextDouble()) / FillCount);
                var amount = Math.Min(existingAmount + fillAmount, d.MaximumOfferAmount);
                var pricePerUnit = CalculateItemMinimalPrice(id);
                var item = storeBlock.CreateStoreItem(id, amount, pricePerUnit, StoreItemTypes.Offer);
                storeBlock.InsertStoreItem(item);

                MyLog.Default.Debug($"[HnzPveSeason] UpdateStoreItems() offer; faction: {_faction.Tag}, item: {id}, amount: {amount}, existing amount: {existingAmount}, fill amount: {fillAmount}");
            }

            foreach (var itemId in _factionType.OrdersList)
            {
                MyPhysicalItemDefinition d;
                if (!MyDefinitionManager.Static.TryGetDefinition(itemId, out d)) continue;

                var initAmount = (int)MathHelper.Lerp(d.MinimumOrderAmount, d.MaximumOrderAmount, MyRandom.Instance.NextDouble());
                var amount = existingOrders.GetValueOrDefault(itemId, initAmount);
                var pricePerUnit = CalculateItemMinimalPrice(d.Id);
                var item = storeBlock.CreateStoreItem(d.Id, amount, pricePerUnit, StoreItemTypes.Order);
                storeBlock.InsertStoreItem(item);

                MyLog.Default.Debug($"[HnzPveSeason] UpdateStoreItems() order; faction: {_faction.Tag}, item: {d.Id}, amount: {amount}");
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
        static int CalculateItemMinimalPrice(MyDefinitionId itemId)
        {
            MyPhysicalItemDefinition myPhysicalItemDefinition;
            if (MyDefinitionManager.Static.TryGetDefinition(itemId, out myPhysicalItemDefinition) && myPhysicalItemDefinition.MinimalPricePerUnit > 0)
            {
                return myPhysicalItemDefinition.MinimalPricePerUnit;
            }

            MyBlueprintDefinitionBase myBlueprintDefinitionBase;
            if (!MyDefinitionManager.Static.TryGetBlueprintDefinitionByResultId(itemId, out myBlueprintDefinitionBase))
            {
                return 0;
            }

            var num = myPhysicalItemDefinition.IsIngot ? 1f : MyAPIGateway.Session.AssemblerEfficiencyMultiplier;
            var num2 = 0;
            foreach (var item in myBlueprintDefinitionBase.Prerequisites)
            {
                var num3 = CalculateItemMinimalPrice(item.Id);
                var num4 = (float)item.Amount / num;
                num2 += (int)(num3 * num4);
            }

            var num5 = myPhysicalItemDefinition.IsIngot ? MyAPIGateway.Session.RefinerySpeedMultiplier : MyAPIGateway.Session.AssemblerSpeedMultiplier;
            foreach (var item2 in myBlueprintDefinitionBase.Results)
            {
                if (item2.Id != itemId) continue;

                var amount = (float)item2.Amount;
                if (amount == 0f) continue;

                var num7 = 1f + (float)Math.Log(myBlueprintDefinitionBase.BaseProductionTimeInSeconds + 1f) / num5;
                var num8 = (int)(num2 * (1f / amount) * num7);
                return num8;
            }

            return 0;
        }
    }
}