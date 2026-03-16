using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using HnzCoopSeason.Merchants;
using HnzCoopSeason.Missions;
using HnzCoopSeason.Orks;
using HnzCoopSeason.POI;
using HnzUtils;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Serialization;
using VRage.Utils;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class SessionConfig
    {
        const string FileName = "HnzCoopSeason.Config.xml";

        [XmlElement]
        public float EncounterClearance = 500;

        [XmlElement]
        public int InvasionIntervalHours = 4;

        [XmlElement]
        public int EconomyUpdateIntervalMinutes = 20;

        [XmlElement]
        public int DefaultEconomyUpdateIntervalToFillItems = 30;


        [XmlElement]
        public double PoiMapCenterZ;

        [XmlElement]
        public float PoiMapRadius = 10000000;

        [XmlElement]
        public int PoiCountPerAxis = 4;

        [XmlElement]
        public int MaxProgressLevel = 5;

        [XmlElement]
        public float EncounterRadius = 10000;


        [XmlElement]
        public int ExposedPoiCount = 3;


        [XmlArray]
        [XmlArrayItem("Mission")]
        public MissionConfig[] Missions =
        {
            new MissionConfig
            {
                Type = MissionType.Acquisition,
                Title = "Unironically, not enough stones",
                Description = "Our pet bird just threw up due to a hangover the other day and she needs a bulk of stones to reset her gastroliths. Please collect as much as you can.",
                Goal = 2400000,
                CustomData = new SerializableDictionary<string, string>(new Dictionary<string, string>
                {
                    { MissionUtils.AcquisitionItemTypeKey, "MyObjectBuilder_Ore/Stone" }
                })
            },
            new MissionConfig
            {
                Type = MissionType.Acquisition,
                Title = "Unironically, not enough stones",
                Description = "Our pet bird just threw up due to a hangover the other day and she needs a bulk of stones to reset her gastroliths. Please collect as much as you can.",
                Goal = 2400000,
                CustomData = new SerializableDictionary<string, string>(new Dictionary<string, string>
                {
                    { MissionUtils.AcquisitionItemTypeKey, "MyObjectBuilder_Ore/Stone" }
                })
            }
        };

        [XmlArray]
        [XmlArrayItem("Ork")]
        public PoiOrkConfig[] Orks = { new PoiOrkConfig() };

        [XmlElement]
        public string RespawnDatapadTextFormat = "Come here: {0}";

        [XmlElement]
        public float OrksDamageManipulationScale = 0.5f;

        [XmlArray]
        [XmlArrayItem("Poi")]
        public PoiConfig[] PlanetaryPois = { new PoiConfig() };


        [XmlElement]
        public double PoiMapCenterX;

        [XmlElement]
        public double PoiMapCenterY;


        [XmlArray]
        [XmlArrayItem("PoiMerchant")]
        public PoiMerchantConfig[] PoiMerchants = { new PoiMerchantConfig() };

        [XmlArray("ProgressionLevels")]
        [XmlArrayItem("Level")]
        public ProgressionLevelConfig[] ProgressionLevelList =
        {
            new ProgressionLevelConfig(1, 1),
            new ProgressionLevelConfig(2, 1),
            new ProgressionLevelConfig(3, 2),
            new ProgressionLevelConfig(4, 3),
            new ProgressionLevelConfig(5, 4)
        };


        [XmlArray]
        [XmlArrayItem("StoreItem")]
        public StoreItemConfig[] StoreOfferItems = { new StoreItemConfig() };

        [XmlArray]
        [XmlArrayItem("StoreItem")]
        public StoreItemConfig[] StoreOrderItems = { new StoreItemConfig() };

        public static SessionConfig Instance { get; private set; }

        [XmlIgnore]
        public IReadOnlyDictionary<int, ProgressionLevelConfig> ProgressionLevels { get; private set; }

        [XmlIgnore]
        public IReadOnlyDictionary<MyObjectBuilder_PhysicalObject, StoreItemConfig> StoreOfferItemBuilders { get; private set; }

        [XmlIgnore]
        public IReadOnlyDictionary<MyObjectBuilder_PhysicalObject, StoreItemConfig> StoreOrderItemBuilders { get; private set; }

        void Initialize()
        {
            ProgressionLevels = ProgressionLevelList.ToDictionary(c => c.Level);
            StoreOfferItemBuilders = ParseStoreItems(StoreOfferItems, true);
            StoreOrderItemBuilders = ParseStoreItems(StoreOrderItems, false);
        }

        Dictionary<MyObjectBuilder_PhysicalObject, StoreItemConfig> ParseStoreItems(StoreItemConfig[] itemConfigs, bool isOffer)
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

        void FixStoreItemValues(StoreItemConfig config, MyPhysicalItemDefinition definition, bool isOffer)
        {
            var maxAmount = isOffer ? definition.MaximumOfferAmount : definition.MaximumOrderAmount;
            FixIntValue(ref config.MaxAmount, maxAmount);
            FixIntValue(ref config.PricePerUnit, definition.MinimalPricePerUnit);
            FixIntValue(ref config.AmountPerUpdate, (int)Math.Ceiling((double)maxAmount / DefaultEconomyUpdateIntervalToFillItems));
        }

        static void FixIntValue(ref int value, int defaultValue)
        {
            if (value == 0) value = defaultValue;
        }

        public static void Load()
        {
            SessionConfig content;
            if (!VRageUtils.TryLoadStorageXmlFile(FileName, out content)) content = new SessionConfig();

            content.Initialize();
            Instance = content;
            Save();
        }

        public static void Save()
        {
            if (Instance == null)
            {
                MyLog.Default.Error("[HnzCoopSeason] config failed to save; instance null");
                return;
            }

            VRageUtils.SaveStorageFile(FileName, Instance);
        }
    }
}