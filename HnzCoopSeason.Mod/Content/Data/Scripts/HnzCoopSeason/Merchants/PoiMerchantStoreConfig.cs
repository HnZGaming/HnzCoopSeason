using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using HnzUtils;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Utils;

namespace HnzCoopSeason.Merchants
{
    public sealed class PoiMerchantStoreConfig
    {
        [XmlArray]
        [XmlArrayItem("StoreItem")]
        public StoreItemConfig[] Offers = { new StoreItemConfig() };

        [XmlArray]
        [XmlArrayItem("StoreItem")]
        public StoreItemConfig[] Orders = { new StoreItemConfig() };

        [XmlIgnore]
        public IReadOnlyDictionary<MyObjectBuilder_PhysicalObject, StoreItemConfig> OfferBuilders { get; private set; }

        [XmlIgnore]
        public IReadOnlyDictionary<MyObjectBuilder_PhysicalObject, StoreItemConfig> OrderBuilders { get; private set; }

        public void Initialize()
        {
            OfferBuilders = ParseStoreItems(Offers, true);
            OrderBuilders = ParseStoreItems(Orders, false);
        }

        static Dictionary<MyObjectBuilder_PhysicalObject, StoreItemConfig> ParseStoreItems(StoreItemConfig[] itemConfigs, bool isOffer)
        {
            var results = new Dictionary<MyObjectBuilder_PhysicalObject, StoreItemConfig>();
            var duplicates = new HashSet<MyDefinitionId>();
            foreach (var c in itemConfigs)
            {
                MyDefinitionId id;
                if (!MyDefinitionId.TryParse($"{c.Type}/{c.Subtype}", out id))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] misformatted store item config: {c}");
                    continue;
                }

                if (duplicates.Contains(id))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] duplicate store item config: {c}");
                    continue;
                }

                MyObjectBuilder_PhysicalObject builder;
                if (!VRageUtils.TryCreatePhysicalObjectBuilder(id, out builder))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] builder not found: {c}");
                    continue;
                }

                MyPhysicalItemDefinition itemDefinition;
                if (!MyDefinitionManager.Static.TryGetDefinition(id, out itemDefinition))
                {
                    MyLog.Default.Error($"[HnzCoopSeason] item definition not found: {c}");
                    continue;
                }

                FixStoreItemValues(c, itemDefinition, isOffer);

                results.Add(builder, c);
                duplicates.Add(id);

                MyLog.Default.Info($"[HnzCoopSeason] merchant item config loaded: {c}");
            }

            return results;
        }

        static void FixStoreItemValues(StoreItemConfig config, MyPhysicalItemDefinition definition, bool isOffer)
        {
            var maxAmount = isOffer ? definition.MaximumOfferAmount : definition.MaximumOrderAmount;
            FixIntValue(ref config.MaxAmount, maxAmount);
            FixIntValue(ref config.PricePerUnit, definition.MinimalPricePerUnit);
            var updateInterval = SessionConfig.Instance.DefaultEconomyUpdateIntervalToFillItems;
            FixIntValue(ref config.AmountPerUpdate, (int)Math.Ceiling((double)maxAmount / updateInterval));
        }

        static void FixIntValue(ref int value, int defaultValue)
        {
            if (value == 0) value = defaultValue;
        }
    }
}