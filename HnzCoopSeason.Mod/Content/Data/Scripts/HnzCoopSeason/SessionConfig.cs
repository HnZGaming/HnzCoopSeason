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
        public static SessionConfig Instance { get; private set; }

        [XmlElement]
        public double PoiMapCenterX;

        [XmlElement]
        public double PoiMapCenterY;

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
        public float EncounterClearance = 500;

        [XmlElement]
        public int InvasionIntervalHours = 4;

        [XmlElement]
        public int EconomyUpdateIntervalMinutes = 20;

        [XmlElement]
        public int ExposedPoiCount = 3;

        [XmlElement]
        public string RespawnDatapadTextFormat = "Come here: {0}";

        [XmlArray("ProgressionLevels")]
        [XmlArrayItem("Level")]
        public ProgressionLevelConfig[] ProgressionLevelList =
        {
            new ProgressionLevelConfig(1, 1),
            new ProgressionLevelConfig(2, 1),
            new ProgressionLevelConfig(3, 2),
            new ProgressionLevelConfig(4, 3),
            new ProgressionLevelConfig(5, 4),
        };

        [XmlArray]
        [XmlArrayItem("Poi")]
        public PoiConfig[] PlanetaryPois = { new PoiConfig() };

        [XmlArray]
        [XmlArrayItem("Ork")]
        public PoiOrkConfig[] Orks = { new PoiOrkConfig() };

        [XmlArray]
        [XmlArrayItem("PoiMerchant")]
        public PoiMerchantConfig[] PoiMerchants = { new PoiMerchantConfig() };

        [XmlArray]
        [XmlArrayItem("StoreItem")]
        public StoreItemConfig[] StoreItems = { new StoreItemConfig() };

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
                    { MissionUtils.AcquisitionItemTypeKey, "MyObjectBuilder_Ore/Stone" },
                }),
            },
            new MissionConfig
            {
                Type = MissionType.Acquisition,
                Title = "Unironically, not enough stones",
                Description = "Our pet bird just threw up due to a hangover the other day and she needs a bulk of stones to reset her gastroliths. Please collect as much as you can.",
                Goal = 2400000,
                CustomData = new SerializableDictionary<string, string>(new Dictionary<string, string>
                {
                    { MissionUtils.AcquisitionItemTypeKey, "MyObjectBuilder_Ore/Stone" },
                }),
            },
        };

        [XmlIgnore]
        public IReadOnlyDictionary<int, ProgressionLevelConfig> ProgressionLevels { get; private set; }

        void Initialize()
        {
            ProgressionLevels = ProgressionLevelList.ToDictionary(c => c.Level);
        }

        public static void Load()
        {
            SessionConfig content;
            if (!VRageUtils.TryLoadStorageXmlFile(FileName, out content))
            {
                content = new SessionConfig();
            }

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