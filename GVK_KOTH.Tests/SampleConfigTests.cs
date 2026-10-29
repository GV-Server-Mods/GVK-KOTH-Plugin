using System.IO;
using System.Xml.Serialization;
using GVK.Koth.Core.Config;
using Xunit;

namespace GVK.Koth.Tests
{
    public class SampleConfigTests
    {
        private static string FindSample()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "docs", "GVK_KOTH.sample.cfg")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return Path.Combine(dir.FullName, "docs", "GVK_KOTH.sample.cfg");
        }

        [Fact]
        public void Sample_config_loads_and_has_no_problems()
        {
            KothConfig config;
            using (var reader = File.OpenRead(FindSample()))
                config = (KothConfig)new XmlSerializer(typeof(KothConfig)).Deserialize(reader);

            KothConfig.ApplyDefaults(config);
            Assert.Equal(4, config.Zones.Count);
            Assert.Equal(3, config.Slots.Count);
            Assert.Equal(new[] { 30, 10 }, config.SlotWarningMinutes);
            Assert.Empty(ConfigCheck.Run(config));
        }

        [Fact]
        public void Loading_twice_does_not_duplicate_list_items()
        {
            var serializer = new XmlSerializer(typeof(KothConfig));
            KothConfig first;
            using (var reader = File.OpenRead(FindSample()))
                first = (KothConfig)serializer.Deserialize(reader);

            var text = new StringWriter();
            serializer.Serialize(text, first);
            var second = (KothConfig)serializer.Deserialize(new StringReader(text.ToString()));

            Assert.Equal(new[] { 30, 10 }, second.SlotWarningMinutes);
        }

        [Fact]
        public void Missing_warning_list_gets_the_defaults()
        {
            var config = new KothConfig();
            KothConfig.ApplyDefaults(config);
            Assert.Equal(new[] { 30, 10 }, config.SlotWarningMinutes);
            Assert.NotNull(config.WeeklyRollup);
        }
    }
}
