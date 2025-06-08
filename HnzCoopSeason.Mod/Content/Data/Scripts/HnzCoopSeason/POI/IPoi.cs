using VRageMath;

namespace HnzCoopSeason.POI
{
    public interface IPoi
    {
        string Id { get; }
        Vector3D Position { get; }
        PoiState State { get; }
        bool IsPlanetary { get; }
    }
}