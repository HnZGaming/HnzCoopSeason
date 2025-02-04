using System;
using System.Xml.Serialization;
using VRage.Game;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class ContractConfig
    {
        [XmlAttribute]
        public string Id = "Id";

        [XmlAttribute]
        public ContractType Type = ContractType.Acquisition;

        [XmlAttribute]
        public string ItemId = "Component/Construction";

        [XmlAttribute]
        public string Name = "Acquisition";

        [XmlAttribute]
        public string Description = "Acquisition";

        [XmlAttribute]
        public int Reward = 100;

        [XmlAttribute]
        public int Collateral = 100;

        [XmlAttribute]
        public int Duration = 120;

        [XmlAttribute]
        public int ItemAmount = 10;

        public MyDefinitionId ItemDefinitionId => MyDefinitionId.Parse(ItemId);
    }
}