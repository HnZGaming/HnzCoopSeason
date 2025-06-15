namespace HnzCoopSeason.Missions
{
    public interface IMissionLogic
    {
        Mission Mission { get; }

        void LoadServerProgress(); // read global state into `Mission.Progress`; server only
        void OnClientUpdate(); // called every update loop; client only
        void EvaluateClient();
        bool TrySubmit(); // server only
        void ForceProgress(int progress); // server only
    }
}