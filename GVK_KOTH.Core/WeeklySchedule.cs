using System;
using GVK.Koth.Core.Config;

namespace GVK.Koth.Core
{
    /// <summary>
    /// Weekly timing for slots and the roll-up. All times are UTC; a window may cross midnight or the week's end.
    /// </summary>
    public static class WeeklySchedule
    {
        private const int MinutesPerWeek = 7 * 1440;

        /// <summary>The most recent occurrence of the weekly time at or before utcNow.</summary>
        public static DateTime LastStart(WeeklyTime time, DateTime utcNow)
        {
            var weekStart = utcNow.Date.AddDays(-(int)utcNow.DayOfWeek);
            var start = weekStart.AddMinutes(time.MinuteOfWeek);
            return start > utcNow ? start.AddMinutes(-MinutesPerWeek) : start;
        }

        /// <summary>The next occurrence of the weekly time strictly after utcNow.</summary>
        public static DateTime NextStart(WeeklyTime time, DateTime utcNow)
        {
            return LastStart(time, utcNow).AddMinutes(MinutesPerWeek);
        }

        /// <summary>True while utcNow is inside the window that started at the last occurrence.</summary>
        public static bool IsActive(WeeklyTime time, int durationMinutes, DateTime utcNow, out DateTime start)
        {
            start = LastStart(time, utcNow);
            return durationMinutes > 0 && utcNow < start.AddMinutes(durationMinutes);
        }
    }
}
