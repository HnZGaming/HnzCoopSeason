using System;
using System.Xml.Serialization;
using Sandbox.ModAPI;
using VRage.Utils;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class SessionConfig
    {
        const string FileName = "HnzPveSeason.Config.xml";
        public static SessionConfig Instance { get; private set; }

        [XmlElement]
        public float PoiMapRadius = 10000000;

        [XmlElement]
        public int PoiCountPerAxis = 5;

        [XmlElement]
        public float EncounterRadius = 10000;

        [XmlElement]
        public float RandomInvasionChance = 0.1f;

        [XmlElement]
        public int RandomInvasionInterval = 600;

        [XmlArray]
        [XmlArrayItem("Poi")]
        public PoiConfig[] PlanetaryPois = { new PoiConfig() };

        [XmlArray]
        [XmlArrayItem("Ork")]
        public MesStaticEncounterConfig[] Orks = { new MesStaticEncounterConfig() };

        [XmlArray]
        [XmlArrayItem("Merchant")]
        public MesStaticEncounterConfig[] Merchants = { new MesStaticEncounterConfig() };

        [XmlArray]
        [XmlArrayItem("Contract")]
        public ContractConfig[] Contracts = { new ContractConfig() };

        public static void Load()
        {
            MyLog.Default.Info("[HnzPveSeason] config loading");

            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(FileName, typeof(SessionConfig)))
            {
                try
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(FileName, typeof(SessionConfig)))
                    {
                        var contentText = reader.ReadToEnd();
                        Instance = MyAPIGateway.Utilities.SerializeFromXML<SessionConfig>(contentText);
                        MyLog.Default.Info("[HnzPveSeason] config loaded");
                        return;
                    }
                }
                catch (Exception e)
                {
                    MyLog.Default.Error($"[HnzPveSeason] config failed loading: {e}");
                }
            }

            MyLog.Default.Info("[HnzPveSeason] config creating");
            Instance = new SessionConfig();
            Save();
        }

        public static void Save()
        {
            if (Instance == null)
            {
                MyLog.Default.Error("[HnzPveSeason] config failed to save; instance null");
                return;
            }

            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(SessionConfig)))
                {
                    var xml = MyAPIGateway.Utilities.SerializeToXML(Instance);
                    writer.Write(xml);
                    MyLog.Default.Info("[HnzPveSeason] config saved");
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"[HnzPveSeason] config failed to save: {e}");
            }
        }
    }
}