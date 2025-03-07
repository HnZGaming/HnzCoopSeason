using VRage.Game.ModAPI;

namespace HnzCoopSeason
{
    public interface IPoiObserver
    {
        void Load(IMyCubeGrid[] grids);
        void Unload(bool sessionUnload);
        void Update();
        void OnStateChanged(PoiState state);
    }
}