using System;
using System.Xml.Serialization;
using Sandbox.ModAPI;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class MesGridContext
    {
        const string Prefix = "MesGridContext:";

        [XmlElement]
        public string Id;

        [XmlElement]
        public string MainPrefabId;

        public MesGridContext(string id, string mainPrefabId)
        {
            Id = id;
            MainPrefabId = mainPrefabId;
        }

        public string ToXml()
        {
            return Prefix + MyAPIGateway.Utilities.SerializeToXML(this);
        }

        public static bool FromXml(string text, out MesGridContext context)
        {
            context = null;
            if (!text.StartsWith(Prefix)) return false;

            text = text.Substring(Prefix.Length);
            context = MyAPIGateway.Utilities.SerializeFromXML<MesGridContext>(text);
            return true;
        }
    }
}