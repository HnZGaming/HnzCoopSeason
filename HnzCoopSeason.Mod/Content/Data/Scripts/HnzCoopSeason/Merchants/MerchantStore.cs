using System;
using System.Collections.Generic;
using System.Linq;
using HnzUtils;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;

namespace HnzCoopSeason.Merchants
{
    public sealed class MerchantStore
    {
        readonly string _id;
        readonly IMyCubeGrid _grid;

        public MerchantStore(string id, IMyCubeGrid grid)
        {
            _id = id;
            _grid = grid;
        }

        public void Update(bool fill = false)
        {
            MyLog.Default.Debug("[HnzCoopSeason] merchant {0} update store items; fill: {1}", _id, fill ? "o" : "x");

            var storeBlocks = _grid.GetFatBlocks<IMyStoreBlock>().Where(b => IsStoreBlock(b)).ToArray();
            if (storeBlocks.Length != 1)
            {
                MyLog.Default.Error($"[HnzCoopSeason] merchant{_id} invalid store block count; blocks: {storeBlocks.ToStringSeq()}");
                return;
            }

            var cargoBlocks = _grid.GetFatBlocks<IMyCargoContainer>().Where(b => IsCargoBlock(b)).ToArray();
            if (cargoBlocks.Length == 0)
            {
                MyLog.Default.Error($"[HnzCoopSeason] merchant {_id} no cargo container blocks");
                return;
            }

            var storeBlock = storeBlocks[0];
            var inventory = cargoBlocks[0].GetInventory(0);

            var allExistingItems = new List<IMyStoreItem>();
            storeBlock.GetStoreItems(allExistingItems);

            var existingOffers = new Dictionary<MyDefinitionId, int>();
            foreach (var item in allExistingItems)
            {
                if (item.Item == null) continue; // shouldn't happen
                if (item.StoreItemType != StoreItemTypes.Offer) continue;

                var id = item.Item.Value.ToDefinitionId();
                existingOffers[id] = item.Amount;
            }

            storeBlock.ClearItems();

            var allItems = new Dictionary<MyDefinitionId, int>();
            foreach (var kvp in GetStoreItemConfigs())
            {
                var id = kvp.Key;
                var c = kvp.Value;

                var existingAmount = existingOffers.GetValueOrDefault(id, 0);
                if (fill)
                {
                    existingAmount += c.MaxAmount;
                }

                var fillAmount = (int)MathHelper.Lerp(c.MinAmountPerUpdate, c.MaxAmountPerUpdate, MyRandom.Instance.NextDouble());
                var amount = Math.Min(existingAmount + fillAmount, c.MaxAmount);
                var item = storeBlock.CreateStoreItem(id, amount, c.PricePerUnit, StoreItemTypes.Offer);
                storeBlock.InsertStoreItem(item);
                allItems.Increment(id, amount);

                MyLog.Default.Debug("[HnzCoopSeason] UpdateStoreItems(); item: {0}, origin: {1}, delta: {2}", id, existingAmount, amount, fillAmount);
            }

            inventory.Clear();
            foreach (var kvp in allItems)
            {
                var builder = new MyObjectBuilder_Component { SubtypeName = kvp.Key.SubtypeName };
                inventory.AddItems(kvp.Value, builder);
            }
        }

        static bool IsStoreBlock(IMyStoreBlock block)
        {
            return block.BlockDefinition.SubtypeName == "StoreBlock";
        }

        static bool IsCargoBlock(IMyCargoContainer block)
        {
            return block.CustomName.Contains("Cargo");
        }

        static Dictionary<MyDefinitionId, StoreItemConfig> GetStoreItemConfigs()
        {
            var items = SessionConfig.Instance.StoreItems;
            var results = new Dictionary<MyDefinitionId, StoreItemConfig>();
            foreach (var c in items)
            {
                MyDefinitionId id;
                if (!MyDefinitionId.TryParse($"{c.Type}/{c.Subtype}", out id))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] invalid store item config: {c}");
                    continue;
                }

                if (results.ContainsKey(id))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] duplicate store item config: {c}");
                    continue;
                }

                results.Add(id, c);
            }

            return results;
        }
    }
}