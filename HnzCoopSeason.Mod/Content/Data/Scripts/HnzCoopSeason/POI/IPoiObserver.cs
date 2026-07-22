using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.POI
{
    public interface IPoiObserver
    {
        void Load(IMyCubeGrid[] grids);
        void Unload(bool sessionUnload);
        void Update();
        void OnStateChanged(PoiState state);

        // true world position of the observed entity (grid centre / origin), no marker offset.
        bool TryGetPosition(out Vector3D position);

        // where the gps marker should sit: TryGetPosition stood off so it clears the entity's hull.
        bool TryGetMarkerPosition(out Vector3D position);
    }
}