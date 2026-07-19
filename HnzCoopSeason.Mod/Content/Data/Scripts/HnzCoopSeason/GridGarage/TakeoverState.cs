using System;
using System.Xml.Serialization;

namespace GridStorage.API
{
    /// <summary>
    /// Takeover XML document for each grid's mod storage.
    /// Grid Garage mod does NOT compute takeover by itself; other mods must provide it.
    /// This XML document must be present in both the server and clients.
    /// If the document isn't present in the client, the scan UI will fail. If not present in the server, verification will fail.
    /// Note that mod storage values are synchronized upon entity replication and never after.
    /// </summary>
    [Serializable]
    public sealed class TakeoverState
    {
        public static readonly Guid ModStorageKey = Guid.Parse("acf6bfbf-a301-426e-ad70-40d93d693b50");

        /// <summary>
        /// Ownership of control-type blocks
        /// </summary>
        [XmlElement]
        public bool CanTakeOver;

        /// <summary>
        /// Relevant iff `CanTakeOver` is true.
        /// 0 if anyone can take over the grid,
        /// otherwise a specific player group can.
        /// Player group ID is either:
        /// - Faction ID if a player is part of one,
        /// - Player ID otherwise.
        /// </summary>
        [XmlElement]
        public long TakeoverPlayerGroup;

        /* CUSTOM PROPERTIES BELOW */

        /// <summary>
        /// Player Group IDs for control-type blocks.
        /// </summary>
        [XmlArray]
        [XmlArrayItem("Controller")]
        public long[] Controllers = Array.Empty<long>();

        /// <summary>
        /// High-water mark of <see cref="Controllers"/>.Length, never decreasing.
        /// Grinding a control block removes it from Controllers entirely, so a meter that
        /// divides by the live length renormalizes to the smaller set and reads 3/3 the
        /// instant you remove one of four -- erasing the progress that removal represents.
        /// 0 on documents written before this field existed; treat it as "use the live length".
        /// </summary>
        [XmlElement]
        public int MaxControllers;
    }
}