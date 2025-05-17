namespace HnzCoopSeason.Missions
{
    public interface IMissionLogic
    {
        Mission Mission { get; }
        bool CanSubmit { get; }
        string SubmitNote { get; }
        string Status { get; }
        int DeltaProgress { get; }

        void Update(MissionBlock missionBlockOrNotFound);
        void UpdateFull(MissionBlock missionBlockOrNotFound);
        bool TryProcessSubmit(); // server only
    }
}