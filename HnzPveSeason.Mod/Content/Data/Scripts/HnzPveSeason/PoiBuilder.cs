using System;
using System.Xml.Serialization;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class PoiBuilder
    {
        [XmlAttribute]
        public PoiState CurrentState;

        [XmlAttribute]
        public int OrkEncounterIndex;
    }
}