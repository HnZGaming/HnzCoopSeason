namespace HnzCoopSeason.HudUtils
{
    /// <summary>Data for one screen-top meter view (e.g. PEACEMETER, CAPMETER).</summary>
    public sealed class MeterState
    {
        public string Title;
        public string BarName;
        public double Progress; // 0..1
        public string ValueText;
        public string Subtitle;
        public string SubtitleHighlight; // substring of Subtitle
        public string Description;
        public bool ShowInfoIcon;
        public bool HealthStyle; // bar drains instead of filling
        public string CompleteText; // stamped inside the track once complete
    }
}
