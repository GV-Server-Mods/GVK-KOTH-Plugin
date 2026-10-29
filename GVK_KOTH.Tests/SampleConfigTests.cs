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

            Assert.Equal(4, config.Zones.Count);
            Assert.Equal(3, config.Slots.Count);
            Assert.Empty(ConfigCheck.Run(config));
        }
    }
}
