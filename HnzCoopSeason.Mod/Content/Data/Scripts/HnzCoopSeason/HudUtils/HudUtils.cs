using System;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;

namespace HnzCoopSeason.HudUtils
{
    public static class HudUtils
    {
        public static void SetDisplayMode(this WindowBase self, HudDisplayMode mode)
        {
            self.Visible = mode != HudDisplayMode.Hidden;
            HudMain.EnableCursor = mode == HudDisplayMode.VisibleWithCursor;
        }

        public static HudDisplayMode Increment(this HudDisplayMode self)
        {
            var len = Enum.GetValues(typeof(HudDisplayMode)).Length;
            var next = ((int)self + 1) % len;
            return (HudDisplayMode)next;
        }
    }
}