using System;
using System.Linq;
using GVK.Koth.Core;
using GVK.Koth.Core.Config;
using Xunit;

namespace GVK.Koth.Tests
{
    public class ScoreBookTests
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Points_add_up_and_are_logged()
        {
            var book = new ScoreBook();
            book.Add(1, "AAA", "Alpha", 5, "Zone", "Khar Toba", Now);
            var entry = book.Add(1, "AAA", "Alpha", 5, "Zone", "Khar Toba", Now);
            Assert.Equal(10, book.Get(1).Points);
            Assert.Equal(5, entry.Change);
            Assert.Equal(10, entry.NewTotal);
        }

        [Fact]
        public void Subtracting_never_goes_below_zero()
        {
            var book = new ScoreBook();
            book.Add(1, "AAA", "Alpha", 3, "Zone", "", Now);
            var entry = book.Add(1, "AAA", "Alpha", -10, "API", "Siege: shield broken", Now);
            Assert.Equal(0, book.Get(1).Points);
            Assert.Equal(-3, entry.Change);
        }

        [Fact]
        public void Standings_show_gain_since_last_rollup()
        {
            var book = new ScoreBook();
            book.Add(1, "AAA", "Alpha", 20, "Zone", "", Now);
            book.Add(2, "BBB", "Bravo", 5, "Zone", "", Now);
            book.MarkRollup(Now);
            book.Add(2, "BBB", "Bravo", 30, "Zone", "", Now);

            var standings = book.Standings();
            Assert.Equal("BBB", standings[0].Score.Tag);
            Assert.Equal(30, standings[0].GainSinceRollup);
            Assert.Equal(0, standings[1].GainSinceRollup);
        }

        [Fact]
        public void Csv_quotes_commas_and_quotes()
        {
            var entry = new ScoreLogEntry { TimeUtc = Now, FactionId = 1, Tag = "A,B", Change = 1, Source = "API", Reason = "say \"hi\"", NewTotal = 1 };
            Assert.Equal("2026-10-28 12:00:00,1,\"A,B\",1,API,\"say \"\"hi\"\"\",1", entry.ToCsv());
        }
    }

    public class WeeklyScheduleTests
    {
        private static readonly WeeklyTime Saturday19 = new WeeklyTime { Day = DayOfWeek.Saturday, Hour = 19, Minute = 0 };

        [Fact]
        public void Active_inside_the_window()
        {
            var now = new DateTime(2026, 10, 31, 20, 0, 0, DateTimeKind.Utc); // Saturday
            Assert.True(WeeklySchedule.IsActive(Saturday19, 120, now, out var start));
            Assert.Equal(new DateTime(2026, 10, 31, 19, 0, 0), start);
        }

        [Fact]
        public void Not_active_after_the_window()
        {
            var now = new DateTime(2026, 10, 31, 21, 0, 0, DateTimeKind.Utc);
            Assert.False(WeeklySchedule.IsActive(Saturday19, 120, now, out _));
        }

        [Fact]
        public void Window_can_cross_the_end_of_the_week()
        {
            var lateSaturday = new WeeklyTime { Day = DayOfWeek.Saturday, Hour = 23, Minute = 0 };
            var sundayMorning = new DateTime(2026, 11, 1, 0, 30, 0, DateTimeKind.Utc);
            Assert.True(WeeklySchedule.IsActive(lateSaturday, 120, sundayMorning, out var start));
            Assert.Equal(new DateTime(2026, 10, 31, 23, 0, 0), start);
        }

        [Fact]
        public void Next_start_is_next_week_once_started()
        {
            var now = new DateTime(2026, 10, 31, 19, 0, 0, DateTimeKind.Utc);
            Assert.Equal(new DateTime(2026, 11, 7, 19, 0, 0), WeeklySchedule.NextStart(Saturday19, now));
        }
    }

    public class ConfigCheckTests
    {
        private static KothConfig Valid() => new KothConfig
        {
            Zones =
            {
                new ZoneProfile
                {
                    Name = "KharToba", X = 4115, Y = 38891, Z = 26152,
                    Loot = { new LootItem { TypeId = "Component", SubtypeId = "GVK_SpecialPackage1", Amount = 1 } }
                }
            },
            Slots = { new SlotConfig { Name = "Saturday", Pool = { "KharToba" } } }
        };

        [Fact]
        public void Valid_config_has_no_problems()
        {
            Assert.Empty(ConfigCheck.Run(Valid()));
        }

        [Fact]
        public void Slot_naming_a_missing_zone_is_reported()
        {
            var config = Valid();
            config.Slots[0].Pool.Add("Nowhere");
            Assert.Contains(ConfigCheck.Run(config), p => p.Contains("'Nowhere'"));
        }

        [Fact]
        public void Slot_zone_in_no_pool_is_reported()
        {
            var config = Valid();
            config.Slots.Clear();
            Assert.Contains(ConfigCheck.Run(config), p => p.Contains("never runs"));
        }

        [Fact]
        public void Loot_without_container_is_reported()
        {
            var config = Valid();
            config.Zones[0].LootContainerName = "";
            Assert.Contains(ConfigCheck.Run(config), p => p.Contains("LootContainerName"));
        }

        [Fact]
        public void Duplicate_zone_names_are_reported()
        {
            var config = Valid();
            config.Zones.Add(config.Zones[0]);
            Assert.True(ConfigCheck.Run(config).Count(p => p.Contains("more than one zone")) >= 1);
        }
    }
}
