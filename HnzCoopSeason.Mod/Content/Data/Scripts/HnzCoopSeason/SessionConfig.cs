using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using HnzCoopSeason.Merchants;
using HnzCoopSeason.Orks;
using HnzCoopSeason.POI;
using HnzCoopSeason.POI.Reclaim;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

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

        [XmlIgnore]
        public Vector3D PoiMapCenter => new Vector3D(PoiMapCenterX, PoiMapCenterY, PoiMapCenterZ);

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
        public OrkConfig[] Orks = { new OrkConfig() };

        [XmlArray("PoiMerchants")]
        [XmlArrayItem("PoiMerchant")]
        public MerchantConfig[] Merchants = { new MerchantConfig() };

        [XmlArray]
        [XmlArrayItem("StoreItem")]
        public StoreItemConfig[] StoreItems = { new StoreItemConfig() };

        [XmlIgnore]
        public IReadOnlyDictionary<int, ProgressionLevelConfig> ProgressionLevels { get; private set; }

        void Initialize()
        {
            ProgressionLevels = ProgressionLevelList.ToDictionary(c => c.Level);
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