namespace HnzCoopSeason.HudUtils
{
    /// <summary>
    ///     Data for one screen-top meter view (e.g. PEACEMETER, CAPMETER).
    ///     Views write into this; the RichHud MeterPanel reads the winning state every frame.
    /// </summary>
    public sealed class MeterState
    {
        public string Title;
        public string BarName;
        public double Progress; // 0..1
        public string ValueText; // e.g. "42%" or "2/5"
        public string Subtitle;
        public string SubtitleHighlight; // substring of Subtitle drawn in the alert colour
        public string Description;
        public bool ShowInfoIcon; // draws an (i) badge ahead of the title
        public bool HealthStyle; // red bar that drains as Progress falls, instead of cyan filling to green
        // stamped inside the track once the bar reaches its green "done" state, where the fill has
        // vacated it. names what the player can do next -- in minimal mode the description line is
        // hidden, so this is the only cue left
        public string CompleteText;
    }
}
