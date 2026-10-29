using System.Collections.Generic;

namespace GVK.Koth.Core.Config
{
    /// <summary>
    /// Plugin settings, saved as XML by Torch (Persistent&lt;KothConfig&gt;). Edit the file and run !koth reload.
    /// </summary>
    public class KothConfig
    {
        public bool Enabled { get; set; } = true;

        /// <summary>Game ticks between zone checks (60 = about once a second).</summary>
        public int UpdateIntervalTicks { get; set; } = 60;

        public string DiscordWebhookUrl { get; set; } = "";
        public string DiscordAdminWebhookUrl { get; set; } = "";

        /// <summary>Optional role mention for slot warnings, e.g. &lt;@&amp;123456789&gt;.</summary>
        public string DiscordSlotWarningPing { get; set; } = "";

        /// <summary>Minutes before a slot opens to warn players.</summary>
        public List<int> SlotWarningMinutes { get; set; } = new List<int> { 30, 10 };

        /// <summary>Weekly Discord roll-up time (UTC).</summary>
        public WeeklyTime WeeklyRollup { get; set; } = new WeeklyTime { Day = System.DayOfWeek.Monday, Hour = 0, Minute = 0 };

        public List<ZoneProfile> Zones { get; set; } = new List<ZoneProfile>();
        public List<SlotConfig> Slots { get; set; } = new List<SlotConfig>();
    }
}
