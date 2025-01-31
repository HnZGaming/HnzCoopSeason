using System;
using ProtoBuf;
using VRageMath;

namespace HnzPveSeason.FlashGPS
{
    [Serializable]
    [ProtoContract]
    public sealed class FlashGpsSource
    {
        [ProtoMember(1)]
        public long Id { get; set; }

        [ProtoMember(2)]
        public Vector3D Position { get; set; }

        [ProtoMember(3)]
        public Color Color { get; set; }

        [ProtoMember(4, IsRequired = false)]
        public string Name { get; set; }

        [ProtoMember(5, IsRequired = false)]
        public string Description { get; set; }

        /// <summary>
        /// Seconds until this GPS entity is removed from the HUD
        /// unless another source is pushed with the same ID.
        /// If set to 0, the GPS entity will stay in the HUD indefinitely.
        /// </summary>
        /// <remarks>
        /// To remove GPS entities from client HUD "immediately",
        /// send a remove message using the API.
        /// </remarks>
        [ProtoMember(6, IsRequired = false)]
        public float DecaySeconds { get; set; }

        /// <summary>
        /// Radius of this GPS to propagate to players based on character positions.
        /// If set to 0, every player will receive this GPS.
        /// </summary>
        /// <remarks>
        /// Clients will stop receiving this GPS if the character has moved outside the radius.
        /// To ensure that the GPS is removed from HUD, use `DecaySeconds`.
        /// </remarks>
        [ProtoMember(7, IsRequired = false)]
        public double Radius { get; set; } // 0 -> everyone

        /// <summary>
        /// Entity ID that the client HUD must attach this GPS to if replicated.
        /// If set to 0, the GPS will be shown at `Position`.
        /// </summary>
        /// <remarks>
        /// Until the entity is replicated, client will move the GPS in smooth interpolation of `Position`
        /// so that the player will see the GPS "moving" on HUD as following the entity.
        /// `Position` must be set on server so that this interpolation actually takes place.
        /// </remarks>
        [ProtoMember(8, IsRequired = false)]
        public long EntityId { get; set; } // 0 -> won't snap

        [ProtoMember(9, IsRequired = false)]
        public int PromoteLevel { get; set; } // 0 -> everyone

        /// <summary>
        /// List of player ID's who shouldn't receive this GPS.
        /// If not set (null), every player will receive this GPS.
        /// </summary>
        [ProtoMember(10, IsRequired = false)]
        public ulong[] ExcludedPlayers { get; set; } // null -> everyone

        /// <summary>
        /// List of player ID's who will receive this GPS.
        /// If not set (null), every client will receive this GPS.
        /// </summary>
        [ProtoMember(11, IsRequired = false)]
        public ulong[] TargetPlayers { get; set; } // null -> everyone
        
        [ProtoMember(12, IsRequired = false)]
        public bool SuppressSound { get; set; }
    }
}