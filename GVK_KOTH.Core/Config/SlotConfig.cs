using System;
using System.Collections.Generic;

namespace GVK.Koth.Core.Config
{
    /// <summary>
    /// A weekly KOTH slot. At its start time it picks one zone from Pool at random and runs it for DurationMinutes.
    /// </summary>
    public class SlotConfig
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public WeeklyTime Start { get; set; } = new WeeklyTime();
        public int DurationMinutes { get; set; } = 120;

        /// <summary>Zone profile names to pick from.</summary>
        public List<string> Pool { get; set; } = new List<string>();
    }

    /// <summary>A day and time of the week, in UTC.</summary>
    public class WeeklyTime
    {
        public DayOfWeek Day { get; set; } = DayOfWeek.Saturday;
        public int Hour { get; set; } = 19;
        public int Minute { get; set; }

        public int MinuteOfWeek => (int)Day * 1440 + Hour * 60 + Minute;

        public override string ToString() => $"{Day} {Hour:00}:{Minute:00} UTC";
    }
}
