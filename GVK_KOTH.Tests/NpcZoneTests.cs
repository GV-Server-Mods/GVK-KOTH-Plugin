using System;
using GVK.Koth.Core;
using Xunit;

namespace GVK.Koth.Tests
{
    public class NpcTagTests
    {
        [Theory]
        [InlineData("GVK-KOTH=WreckShort;", "WreckShort")]
        [InlineData("[RivalAI Behavior]\n[BehaviorName:Passive]\nBountyOnKill=1000;\nGVK-KOTH=FlyingKOTH;\n", "FlyingKOTH")]
        [InlineData("gvk-koth = Bunker ;", "Bunker")]
        [InlineData("GVK-KOTH=NoSemicolon", "NoSemicolon")]
        public void Reads_the_profile_name(string customData, string expected)
        {
            Assert.Equal(expected, NpcTag.Read(customData));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("BountyOnKill=1000;")]
        [InlineData("[GVK-KOTH-Link]")]
        [InlineData("[GVK-KOTH-Link:KharToba]")]
        [InlineData("GVK-KOTH=;")]
        public void Ignores_data_without_a_KOTH_tag(string customData)
        {
            Assert.Null(NpcTag.Read(customData));
        }
    }

    public class NpcLifetimeTests
    {
        private static readonly DateTime Spawn = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Runs_within_its_lifetime()
        {
            var npc = new NpcZoneSave { FirstSeenUtc = Spawn };
            Assert.True(npc.WithinLifetime(30, Spawn.AddMinutes(29)));
            Assert.False(npc.WithinLifetime(30, Spawn.AddMinutes(30)));
        }

        [Fact]
        public void Zero_lifetime_never_runs_out()
        {
            var npc = new NpcZoneSave { FirstSeenUtc = Spawn };
            Assert.True(npc.WithinLifetime(0, Spawn.AddDays(30)));
        }

        [Fact]
        public void Ended_zone_never_runs_again()
        {
            var npc = new NpcZoneSave { FirstSeenUtc = Spawn, Ended = true };
            Assert.False(npc.WithinLifetime(0, Spawn));
        }

        [Fact]
        public void Each_grid_saves_its_own_bar()
        {
            var a = new NpcZoneSave { GridId = 1, Profile = "Wreck" };
            var b = new NpcZoneSave { GridId = 2, Profile = "Wreck" };
            Assert.NotEqual(a.SaveKey, b.SaveKey);
        }
    }
}
