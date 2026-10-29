using System.Collections.Generic;
using GVK.Koth.Core;
using Xunit;

namespace GVK.Koth.Tests
{
    public class CaptureCurveTests
    {
        [Theory]
        [InlineData(0, 0.0)]
        [InlineData(1, 1.0)]
        [InlineData(2, 1.2727)]
        [InlineData(3, 1.4211)]
        [InlineData(5, 1.5854)]
        public void Speed_matches_the_old_mod(int players, double expected)
        {
            Assert.Equal(expected, CaptureCurve.Speed(players), 3);
        }

        [Fact]
        public void Speed_never_reaches_2x()
        {
            Assert.True(CaptureCurve.Speed(1000) < 2.0);
        }
    }

    public class ZoneCaptureTests
    {
        private static CaptureRules KharToba() => new CaptureRules
        {
            CaptureSeconds = 300,
            MinPlayers = 1,
            IdleDrainPlayers = 3,
            PointsPerCompletion = 5,
            PointsPerLoot = 10
        };

        private static CaptureResult Run(ZoneCapture zone, double seconds, params long[] factions)
        {
            CaptureResult last = CaptureResult.None;
            for (int i = 0; i < seconds; i++)
            {
                var r = zone.Tick(1, factions);
                if (r.Completed) last = r;
            }
            return last;
        }

        [Fact]
        public void One_player_fills_the_bar_in_capture_seconds()
        {
            var zone = new ZoneCapture(KharToba());
            Assert.False(Run(zone, 299, 1).Completed);
            var r = zone.Tick(1, new long[] { 1 });
            Assert.True(r.Completed);
            Assert.Equal(new List<long> { 1 }, r.ScoringFactions);
            Assert.Equal(5, r.PointsEach);
            Assert.Equal(0, zone.Progress);
        }

        [Fact]
        public void Two_factions_share_one_bar_and_both_score()
        {
            var zone = new ZoneCapture(KharToba());
            // Two players, any faction: 1.27x, so 300 s takes about 236 s.
            var r = Run(zone, 236, 1, 2);
            Assert.True(r.Completed);
            Assert.Equal(new List<long> { 1, 2 }, r.ScoringFactions);
        }

        [Fact]
        public void Two_factions_completing_once_drop_one_loot()
        {
            var zone = new ZoneCapture(KharToba());
            var r = Run(zone, 236, 1, 2);
            Assert.Equal(1, r.LootDrops);
            Assert.Equal(0, zone.LootCounter);
        }

        [Fact]
        public void One_faction_needs_two_completions_for_loot()
        {
            var zone = new ZoneCapture(KharToba());
            Assert.Equal(0, Run(zone, 300, 1).LootDrops);
            Assert.Equal(5, zone.LootCounter);
            Assert.Equal(1, Run(zone, 300, 1).LootDrops);
        }

        [Fact]
        public void Empty_zone_drains_at_three_player_speed()
        {
            var zone = new ZoneCapture(KharToba());
            Run(zone, 100, 1);
            zone.Tick(10, new long[0]);
            Assert.Equal(100 - 10 * CaptureCurve.Speed(3), zone.Progress, 3);
        }

        [Fact]
        public void Drain_stops_at_zero()
        {
            var zone = new ZoneCapture(KharToba());
            Run(zone, 10, 1);
            zone.Tick(100, new long[0]);
            Assert.Equal(0, zone.Progress);
        }

        [Fact]
        public void Below_min_players_the_bar_drains()
        {
            var rules = KharToba();
            rules.MinPlayers = 2;
            var zone = new ZoneCapture(rules);
            zone.Tick(50, new long[] { 1 });
            Assert.Equal(0, zone.Progress);
        }

        [Fact]
        public void Late_arrival_misses_out_with_min_presence()
        {
            var rules = KharToba();
            rules.MinPresenceShare = 0.25;
            var zone = new ZoneCapture(rules);
            Run(zone, 290, 1);
            var r = Run(zone, 10, 1, 2); // faction 2 arrives for the last few seconds
            Assert.True(r.Completed);
            Assert.Equal(new List<long> { 1 }, r.ScoringFactions);
        }

        [Fact]
        public void Faction_that_left_before_completion_does_not_score()
        {
            var zone = new ZoneCapture(KharToba());
            Run(zone, 200, 1, 2);
            var r = Run(zone, 200, 1);
            Assert.True(r.Completed);
            Assert.Equal(new List<long> { 1 }, r.ScoringFactions);
        }

        [Fact]
        public void Encampment_ignores_participant_count()
        {
            var rules = KharToba();
            rules.FlatSpeed = true;
            rules.CaptureSeconds = 3600;
            var zone = new ZoneCapture(rules);
            zone.Tick(1000, new long[] { 1, 1, 2, 3 });
            Assert.Equal(1000, zone.Progress, 3);
        }
    }
}
