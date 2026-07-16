using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using HnzCoopSeason.Merchants;
using HnzCoopSeason.Missions;
using HnzCoopSeason.Orks;
using HnzCoopSeason.POI;
using HnzUtils;
using VRage.Serialization;
using VRage.Utils;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class SessionConfig
    {
        const string FileName = "HnzCoopSeason.Config.xml";

        static readonly ProgressionLevelConfig FallbackLevel = new ProgressionLevelConfig(); // multipliers default to 1 = no effect

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

        [XmlArray]
        [XmlArrayItem("MerchantStore")]
        public PoiMerchantStoreConfig[] MerchantStores = { new PoiMerchantStoreConfig() };

        [XmlElement]
        public string RespawnDatapadTextFormat = "Come here: {0}";

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
            new ProgressionLevelConfig(1, 1, 1f, 1f),
            new ProgressionLevelConfig(2, 1, 1f, 1f),
            new ProgressionLevelConfig(3, 1, 1f, 1f),
            new ProgressionLevelConfig(4, 1, 1f, 1f),
            new ProgressionLevelConfig(5, 1, 1f, 1f)
        };

        public static SessionConfig Instance { get; private set; }

        [XmlIgnore]
        public IReadOnlyDictionary<int, ProgressionLevelConfig> ProgressionLevels { get; private set; }

        public ProgressionLevelConfig GetProgressionLevel(int level)
        {
            ProgressionLevelConfig c;
            return ProgressionLevels.TryGetValue(level, out c) ? c : FallbackLevel;
        }

        void Initialize()
        {
            ProgressionLevels = ProgressionLevelList.ToDictionary(c => c.Level);
            foreach (var m in MerchantStores)
            {
                m.Initialize();
            }
        }

        public static void Load()
        {
            SessionConfig content;
            if (!VRageUtils.TryLoadStorageXmlFile(FileName, out content)) content = new SessionConfig();

            Instance = content;
            content.Initialize();
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