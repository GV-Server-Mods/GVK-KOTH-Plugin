using GVK.Koth.Core;
using Xunit;

namespace GVK.Koth.Tests
{
    public class OfflineSafeZoneTests
    {
        [Theory]
        [InlineData("(FSZ)_ABC_123456789", true)]
        [InlineData("(DSZ)_ABC_123456789", true)]
        [InlineData("(FSZ)_ABC_TempZone", true)]
        [InlineData("SafeZone", false)]
        [InlineData("My (FSZ) base", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Only_the_mod_zone_names_count(string name, bool expected)
        {
            Assert.Equal(expected, OfflineSafeZones.IsOfflineZone(name));
        }

        [Fact]
        public void Grid_id_is_the_last_part()
        {
            Assert.Equal(123456789L, OfflineSafeZones.GridId("(FSZ)_ABC_123456789"));
        }

        [Fact]
        public void A_tag_with_an_underscore_still_gives_the_grid_id()
        {
            Assert.Equal(42L, OfflineSafeZones.GridId("(FSZ)_A_B_42"));
        }

        [Theory]
        [InlineData("(FSZ)_ABC_TempZone")]
        [InlineData("(FSZ)")]
        [InlineData("Station_42")]
        public void No_grid_id_gives_zero(string name)
        {
            Assert.Equal(0L, OfflineSafeZones.GridId(name));
        }
    }
}
