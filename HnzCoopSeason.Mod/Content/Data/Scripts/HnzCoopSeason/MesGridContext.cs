using System;
using System.Xml.Serialization;
using Sandbox.ModAPI;

namespace HnzCoopSeason
{
    [Serializable]
    public sealed class MesGridContext
    {
        const string Prefix = "MesGridContext:";

        [XmlElement]
        public string Id;

        [XmlElement]
        public bool IsMainSpawn;

        // serializer
        public MesGridContext()
        {
        }

        public MesGridContext(string id, bool isMainSpawn)
        {
            IsMainSpawn = isMainSpawn;
            Id = id;
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