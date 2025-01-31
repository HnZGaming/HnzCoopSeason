using System;
using System.Xml.Serialization;
using VRageMath;

namespace HnzPveSeason
{
    [Serializable]
    public sealed class PoiConfig
    {
        [XmlAttribute]
        public string Id = "Dummy";

        [XmlAttribute]
        public double X;

        [XmlAttribute]
        public double Y;

        [XmlAttribute]
        public double Z;

        [XmlIgnore]
        public Vector3D Position
        {
            get { return new Vector3D(X, Y, Z); }
            set
            {
                X = value.X;
                Y = value.Y;
                Z = value.Z;
            }
        }
    }
}