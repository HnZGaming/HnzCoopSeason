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

        // the spawned entity's position
        Vector3D GetEntityPosition();

        // GetEntityPosition with offset to clear the entity's hull (jump target).
        Vector3D GetMarkerPosition();
    }
}