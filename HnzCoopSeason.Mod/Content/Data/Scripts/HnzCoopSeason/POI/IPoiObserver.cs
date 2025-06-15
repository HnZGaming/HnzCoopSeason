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
        bool TryGetPosition(out Vector3D position);
    }
}