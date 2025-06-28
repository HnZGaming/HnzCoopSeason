using System;
using System.Xml.Serialization;

namespace GridStorage.API
{
    /// <summary>
    /// Takeover state in a grid's mod storage as an XML document.
    /// Grid Garage mod does NOT calculate takeover state by itself.
    /// Other mods must calculate it and save the result in each grid's mod storage.
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

        // Player Group ID is either:
        // - faction ID if a player is part of a faction,
        // - otherwise player ID
        // 0 indicates an unowned block
        [XmlArray]
        [XmlArrayItem("PlayerGroupId")]
        public long[] PlayerGroups = Array.Empty<long>();
    }
}