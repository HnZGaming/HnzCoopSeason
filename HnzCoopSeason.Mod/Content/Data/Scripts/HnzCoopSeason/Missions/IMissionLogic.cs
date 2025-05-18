namespace HnzCoopSeason.Missions
{
    public interface IMissionLogic
    {
        Mission Mission { get; }
        bool CanSubmit { get; }
        string SubmitNoteText { get; }
        string StatusText { get; }

        void Update(MissionBlock missionBlockOrNotFound);
        void UpdateFull(MissionBlock missionBlockOrNotFound);
        void ProcessSubmit(); // server only
    }
}