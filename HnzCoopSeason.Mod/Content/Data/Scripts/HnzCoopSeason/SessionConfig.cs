using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using HnzCoopSeason.Merchants;
using HnzCoopSeason.Missions;
using HnzCoopSeason.Orks;
using HnzCoopSeason.POI;
using Sandbox.ModAPI;
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
        public ProgressionLevelConfig[] ProgressionLevelsRaw =
        {
            new ProgressionLevelConfig(1, 1, new[]
            {
                new MissionConfig
                {
                    Type = MissionType.Acquisition,
                    Title = "Unironically, not enough stones",
                    Description = "Our pet bird just threw up due to a hangover the other day and she needs a bulk of stones to reset her gastroliths. Please collect as much as you can.",
                    Goal = 2400000,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
            }),
            new ProgressionLevelConfig(2, 1, new[]
            {
                new MissionConfig
                {
                    Type = MissionType.Acquisition,
                    Title = "Unironically, not enough stones",
                    Description = "Our pet bird just threw up due to a hangover the other day and she needs a bulk of stones to reset her gastroliths. Please collect as much as you can.",
                    Goal = 2400000,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
            }),
            new ProgressionLevelConfig(3, 2, new[]
            {
                new MissionConfig
                {
                    Type = MissionType.Acquisition,
                    Title = "Unironically, not enough stones",
                    Description = "Our pet bird just threw up due to a hangover the other day and she needs a bulk of stones to reset her gastroliths. Please collect as much as you can.",
                    Goal = 2400000,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
            }),
            new ProgressionLevelConfig(4, 3, new[]
            {
                new MissionConfig
                {
                    Type = MissionType.Acquisition,
                    Title = "Acquisition",
                    Description = "Description",
                    Goal = 100,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
            }),
            new ProgressionLevelConfig(5, 4, new[]
            {
                new MissionConfig
                {
                    Type = MissionType.Acquisition,
                    Title = "Acquisition",
                    Description = "Description",
                    Goal = 100,
                    AcquisitionItemType = "MyObjectBuilder_Ore/Stone",
                },
            }),
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

        [XmlIgnore]
        public IReadOnlyDictionary<int, ProgressionLevelConfig> ProgressionLevels { get; private set; }

        void Initialize()
        {
            ProgressionLevels = ProgressionLevelsRaw.ToDictionary(c => c.Level);
        }

        public static void Load()
        {
            MyLog.Default.Info("[HnzCoopSeason] config loading");

            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(FileName, typeof(SessionConfig)))
            {
                try
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(FileName, typeof(SessionConfig)))
                    {
                        var contentText = reader.ReadToEnd();
                        Instance = MyAPIGateway.Utilities.SerializeFromXML<SessionConfig>(contentText);
                        Instance.Initialize();
                        MyLog.Default.Info("[HnzCoopSeason] config loaded");
                        return;
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.Error($"[HnzCoopSeason] config failed loading: {e}");
                }
            }

            MyLog.Default.Info("[HnzCoopSeason] config creating");
            Instance = new SessionConfig();
            Instance.Initialize();
            Save();
        }

        public static void Save()
        {
            if (Instance == null)
            {
                MyLog.Default.Error("[HnzCoopSeason] config failed to save; instance null");
                return;
            }

            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(SessionConfig)))
                {
                    var xml = MyAPIGateway.Utilities.SerializeToXML(Instance);
                    writer.Write(xml);
                    MyLog.Default.Info("[HnzCoopSeason] config saved");
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzCoopSeason] config failed to save: {e}");
            }
        }
    }
}