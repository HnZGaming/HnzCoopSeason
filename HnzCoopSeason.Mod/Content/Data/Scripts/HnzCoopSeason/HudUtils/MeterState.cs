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
    }
}
