using System.Collections.Generic;
using VRageMath;

namespace HnzCoopSeason
{
    public interface IPoi
    {
        string Id { get; }
        Vector3D Position { get; }
        PoiState State { get; }
        IReadOnlyList<IPoiObserver> Observers { get; }
        bool IsPlayerAround(float radius);
    }
}