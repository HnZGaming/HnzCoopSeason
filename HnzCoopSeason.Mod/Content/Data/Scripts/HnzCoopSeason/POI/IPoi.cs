using VRage.Game.ModAPI;
using VRageMath;

namespace HnzCoopSeason.POI
{
    public interface IPoi
    {
        string Id { get; }
        Vector3D Position { get; }
        PoiState State { get; }
        bool IsPlanetary { get; }

        bool TryGetEntityPosition(out Vector3D position);

        void Load(IMyCubeGrid[] grids);

        void Unload(bool sessionUnload);

        void Save();

        void Update();

        bool TrySetState(PoiState state);
    }
}