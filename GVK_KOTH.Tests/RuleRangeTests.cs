using System.IO;
using System.Linq;
using System.Xml.Serialization;
using GVK.Koth.Core.Config;
using Xunit;

namespace GVK.Koth.Tests
{
    public class RuleRangeTests
    {
        [Fact]
        public void Max_range_includes_the_drill_range()
        {
            var zone = new ZoneProfile { NoThrusterRange = 3000, NoSafeZoneRange = 4000, NoDrillRange = 5000 };
            Assert.Equal(5000, RuleRanges.Max(zone));
        }

        [Fact]
        public void Max_range_is_zero_with_no_rules()
        {
            Assert.Equal(0, RuleRanges.Max(new ZoneProfile()));
        }

        [Fact]
        public void A_drill_range_alone_turns_the_rules_on()
        {
            Assert.Equal(3000, RuleRanges.Max(new ZoneProfile { NoDrillRange = 3000 }));
        }

        [Fact]
        public void Negative_drill_range_is_reported()
        {
            var config = new KothConfig
            {
                Zones = { new ZoneProfile { Name = "Rules", Mode = ZoneMode.RulesOnly, Activation = ZoneActivation.AlwaysOn, X = 1, NoDrillRange = -1 } }
            };
            Assert.Contains(ConfigCheck.Run(config), p => p.Contains("can't be negative"));
        }

        [Fact]
        public void Empty_drill_list_gets_the_GVK_ToolCore_drills()
        {
            var config = new KothConfig();
            KothConfig.ApplyDefaults(config);
            Assert.Equal(KothConfig.DefaultToolCoreDrillSubtypes, config.ToolCoreDrillSubtypes);
            Assert.True(RuleRanges.IsToolCoreDrill("GVK_LargePrototechDrill", config.ToolCoreDrillSubtypes));
        }

        [Fact]
        public void Other_sorters_are_not_drills()
        {
            var subtypes = KothConfig.DefaultToolCoreDrillSubtypes.ToList();
            Assert.False(RuleRanges.IsToolCoreDrill("LargeBlockConveyorSorter", subtypes));
            Assert.False(RuleRanges.IsToolCoreDrill("GVK_LargeShipWelder", subtypes));
            Assert.False(RuleRanges.IsToolCoreDrill("", subtypes));
            Assert.False(RuleRanges.IsToolCoreDrill(null, subtypes));
        }

        [Fact]
        public void A_drill_list_in_the_file_replaces_the_defaults()
        {
            const string xml = "<KothConfig><ToolCoreDrillSubtypes><string>MyDrill</string></ToolCoreDrillSubtypes></KothConfig>";
            var config = (KothConfig)new XmlSerializer(typeof(KothConfig)).Deserialize(new StringReader(xml));
            KothConfig.ApplyDefaults(config);
            Assert.Equal(new[] { "MyDrill" }, config.ToolCoreDrillSubtypes);
        }

        [Fact]
        public void Sample_KOTH_sites_block_drills_within_3_km_and_Scraptown_has_the_other_rules()
        {
            KothConfig config;
            using (var reader = File.OpenRead(SampleConfigTests.FindSample()))
                config = (KothConfig)new XmlSerializer(typeof(KothConfig)).Deserialize(reader);

            foreach (string name in new[] { "KharToba", "Kalash", "CrashedStarship", "Scraptown" })
                Assert.Equal(3000, config.Zones.Single(z => z.Name == name).NoDrillRange);

            var scraptown = config.Zones.Single(z => z.Name == "Scraptown");
            Assert.Equal(3000, scraptown.NoThrusterRange);
            Assert.Equal(3000, scraptown.NoLargeGridPowerRange);
            Assert.Equal(4000, scraptown.NoSafeZoneRange);
        }

        [Fact]
        public void Sample_Scraptown_is_captured_on_foot_only()
        {
            // Large-grid power and thrusters are off around Scraptown, so a grid-captured zone could never be taken.
            KothConfig config;
            using (var reader = File.OpenRead(SampleConfigTests.FindSample()))
                config = (KothConfig)new XmlSerializer(typeof(KothConfig)).Deserialize(reader);

            var scraptown = config.Zones.Single(z => z.Name == "Scraptown");
            Assert.True(scraptown.OnFoot);
            Assert.False(scraptown.SmallGrids);
            Assert.False(scraptown.LargeGrids);
        }
    }
}
