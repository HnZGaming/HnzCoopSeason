using System.Collections.Generic;
using VRageMath;

namespace HnzCoopSeason.POI
{
    public interface IPoi
    {
        string Id { get; }
        Vector3D Position { get; }
        PoiState State { get; }
        bool IsPlanetary { get; }

        // game-time (GameDateTime.Ticks) the poi last entered Released; 0 if never. persisted.
        long ReleasedAtGameTicks { get; }

        IReadOnlyList<IPoiObserver> Observers { get; }

        // get the position of the POI entity, as opposed to the POI origin,
        // so that players won't get lost when the POI entity spawned in a distance.
        // if no entities have spawned, get the origin position.
        Vector3D GetEntityPosition();
    }
}