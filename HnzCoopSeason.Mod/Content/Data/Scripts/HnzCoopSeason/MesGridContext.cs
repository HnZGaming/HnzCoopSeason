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
        public int Index;

        // serializer
        public MesGridContext()
        {
        }

        public MesGridContext(string id, int index)
        {
            Id = id;
            Index = index;
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