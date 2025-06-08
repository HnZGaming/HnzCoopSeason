using VRageMath;

namespace HnzCoopSeason.POI
{
    public interface IPoi
    {
        string Id { get; }
        Vector3D Position { get; }
        PoiState State { get; }
        bool IsPlanetary { get; }

        // get the position of the POI entity, as opposed to the POI origin,
        // so that players won't get lost when the POI entity spawned in a distance.
        // if no entities have spawned, get the origin position.
        Vector3D GetEntityPosition();
    }
}