using VRage.Game.ModAPI;

namespace HnzPveSeason
{
    public interface IPoiObserver
    {
        void Load(IMyCubeGrid[] grids);
        void Unload();
        void Update();
        void OnStateChanged(PoiState state);
    }
}