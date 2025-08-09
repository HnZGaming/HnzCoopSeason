namespace HnzCoopSeason.POI
{
    public enum PoiState
    {
        Occupied, // Orks are guarding the point
        Released, // Players have reclaimed the point
        Invaded, // Orks have re-reclaimed the point in players' absence
    }
}