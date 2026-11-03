using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using GVK.Koth.Core;
using Xunit;

namespace GVK.Koth.Tests
{
    /// <summary>
    /// Goes through the same KothFileStore calls the plugin's KothData makes for its saves and its start-up load,
    /// using a real folder, with a new store object standing in for a restart.
    /// </summary>
    public class PersistenceTests : IDisposable
    {
        private static readonly DateTime Now = new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

        private readonly string _folder = Path.Combine(Path.GetTempPath(), "gvk-koth-test-" + Guid.NewGuid().ToString("N"));
        private int _unreadableCalls;

        public void Dispose()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        private KothFileStore Start() => new KothFileStore(_folder, (file, backup, ex) => _unreadableCalls++);

        private string[] BadFiles() => Directory.GetFiles(_folder, "*.bad-*");

        [Fact]
        public void Scores_survive_a_restart()
        {
            var run1 = Start();
            var book = new ScoreBook(run1.Read<ScoreData>(KothFileStore.ScoresFile));
            book.Add(1, "AAA", "Alpha <&> Corp", 5, "Zone", "Khar Toba", Now);
            book.Add(1, "AAA", "Alpha <&> Corp", 5, "Zone", "Khar Toba", Now);
            book.Add(2, "BBB", "Bravo", 3, "Admin", "", Now);
            book.MarkRollup(Now);
            book.Add(2, "BBB", "Bravo", 2, "Zone", "Kalash", Now);
            run1.Write(KothFileStore.ScoresFile, KothFileStore.Serialize(book.Data));

            var run2 = Start();
            var loaded = new ScoreBook(run2.Read<ScoreData>(KothFileStore.ScoresFile));

            Assert.Equal(10, loaded.Get(1).Points);
            Assert.Equal("Alpha <&> Corp", loaded.Get(1).Name);
            Assert.Equal(5, loaded.Get(2).Points);
            Assert.Equal(3, loaded.Get(2).PointsAtLastRollup);
            Assert.Equal(Now, loaded.Data.LastRollupUtc.ToUniversalTime());
            Assert.Equal(0, _unreadableCalls);
            Assert.Empty(BadFiles());
        }

        [Fact]
        public void State_survives_a_restart()
        {
            var state = new KothState();
            var zone = state.GetZone("KharToba");
            zone.Progress = 123.5;
            zone.LootCounter = 4;
            zone.PendingLootDrops = 2;
            state.SlotPicks.Add(new SlotPick { Slot = "Saturday", StartUtc = Now, Zone = "Kalash", Spawned = true });
            state.NpcZones.Add(new NpcZoneSave
            {
                GridId = 91662907158886921,
                Profile = "KothCamp",
                FirstSeenUtc = Now,
                LastSeenUtc = Now.AddMinutes(5),
                Ended = true,
                AnnouncedOpen = true
            });
            state.ForcedOn.Add("KharToba");
            state.ForcedOff.Add("Scraptown");

            Start().Write(KothFileStore.StateFile, KothFileStore.Serialize(state));
            var loaded = Start().Read<KothState>(KothFileStore.StateFile);

            Assert.NotNull(loaded);
            var loadedZone = loaded.Zones.Single();
            Assert.Equal("KharToba", loadedZone.Name);
            Assert.Equal(123.5, loadedZone.Progress);
            Assert.Equal(4, loadedZone.LootCounter);
            Assert.Equal(2, loadedZone.PendingLootDrops);

            var pick = loaded.SlotPicks.Single();
            Assert.Equal("Saturday", pick.Slot);
            Assert.Equal("Kalash", pick.Zone);
            Assert.True(pick.Spawned);
            Assert.Equal(Now, pick.StartUtc.ToUniversalTime());

            var npc = loaded.GetNpcZone(91662907158886921);
            Assert.NotNull(npc);
            Assert.Equal("KothCamp#91662907158886921", npc.SaveKey);
            Assert.True(npc.Ended);
            Assert.True(npc.AnnouncedOpen);
            Assert.Equal(Now, npc.FirstSeenUtc.ToUniversalTime());
            Assert.Equal(Now.AddMinutes(5), npc.LastSeenUtc.ToUniversalTime());

            Assert.Equal(new[] { "KharToba" }, loaded.ForcedOn);
            Assert.Equal(new[] { "Scraptown" }, loaded.ForcedOff);
            Assert.Empty(BadFiles());
        }

        [Fact]
        public void Saved_dates_stay_UTC_after_a_load()
        {
            // Lifetimes and slot starts are compared against DateTime.UtcNow, so a load must not turn them into local time.
            var state = new KothState();
            state.SlotPicks.Add(new SlotPick { Slot = "Saturday", StartUtc = Now, Zone = "Kalash" });
            state.NpcZones.Add(new NpcZoneSave { GridId = 7, Profile = "KothCamp", FirstSeenUtc = Now });
            var book = new ScoreBook();
            book.MarkRollup(Now);

            var store = Start();
            store.Write(KothFileStore.StateFile, KothFileStore.Serialize(state));
            store.Write(KothFileStore.ScoresFile, KothFileStore.Serialize(book.Data));
            var loadedState = Start().Read<KothState>(KothFileStore.StateFile);
            var loadedScores = Start().Read<ScoreData>(KothFileStore.ScoresFile);

            Assert.Equal(Now.Ticks, loadedState.SlotPicks[0].StartUtc.Ticks);
            Assert.Equal(Now.Ticks, loadedState.NpcZones[0].FirstSeenUtc.Ticks);
            Assert.Equal(Now.Ticks, loadedScores.LastRollupUtc.Ticks);
            Assert.True(loadedState.NpcZones[0].WithinLifetime(30, Now.AddMinutes(29)));
            Assert.False(loadedState.NpcZones[0].WithinLifetime(30, Now.AddMinutes(31)));
        }

        [Fact]
        public void Saved_files_are_UTF8_and_declare_it()
        {
            var store = Start();
            var book = new ScoreBook();
            book.Add(1, "AAA", "Ünïcode Corp", 5, "Zone", "", Now);
            store.Write(KothFileStore.ScoresFile, KothFileStore.Serialize(book.Data));
            store.Write(KothFileStore.StateFile, KothFileStore.Serialize(new KothState()));

            foreach (string file in new[] { KothFileStore.ScoresFile, KothFileStore.StateFile })
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(_folder, file));
                string text = new UTF8Encoding(false).GetString(bytes);
                Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", text);
                Assert.DoesNotContain("utf-16", text);
            }

            var loaded = new ScoreBook(Start().Read<ScoreData>(KothFileStore.ScoresFile));
            Assert.Equal("Ünïcode Corp", loaded.Get(1).Name);
        }

        [Fact]
        public void Overwriting_replaces_the_file_and_leaves_no_temp_file()
        {
            var store = Start();
            var book = new ScoreBook();
            book.Add(1, "AAA", "Alpha", 5, "Zone", "", Now);
            store.Write(KothFileStore.ScoresFile, KothFileStore.Serialize(book.Data));
            book.Add(1, "AAA", "Alpha", 5, "Zone", "", Now);
            store.Write(KothFileStore.ScoresFile, KothFileStore.Serialize(book.Data));

            Assert.Equal(10, new ScoreBook(Start().Read<ScoreData>(KothFileStore.ScoresFile)).Get(1).Points);
            Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
            Assert.Empty(BadFiles());
        }

        [Fact]
        public void Files_written_by_the_old_build_still_load()
        {
            // The old build serialized through a StringWriter (declares utf-16) and File.WriteAllText wrote UTF-8.
            var book = new ScoreBook();
            book.Add(1, "AAA", "Alpha", 15, "Zone", "", Now);
            using (var writer = new StringWriter())
            {
                new XmlSerializer(typeof(ScoreData)).Serialize(writer, book.Data);
                Assert.Contains("encoding=\"utf-16\"", writer.ToString());
                Directory.CreateDirectory(_folder);
                File.WriteAllText(Path.Combine(_folder, KothFileStore.ScoresFile), writer.ToString());
            }

            var loaded = new ScoreBook(Start().Read<ScoreData>(KothFileStore.ScoresFile));

            Assert.Equal(15, loaded.Get(1).Points);
            Assert.Equal(0, _unreadableCalls);
            Assert.Empty(BadFiles());
        }

        [Fact]
        public void A_missing_file_starts_empty_without_a_backup()
        {
            Assert.Null(Start().Read<ScoreData>(KothFileStore.ScoresFile));
            Assert.Equal(0, _unreadableCalls);
            Assert.Empty(BadFiles());
        }

        [Fact]
        public void A_corrupt_file_is_set_aside_and_reported_once()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(Path.Combine(_folder, KothFileStore.ScoresFile), "this is not xml");

            string reportedBackup = null;
            var store = new KothFileStore(_folder, (file, backup, ex) => reportedBackup = backup);
            Assert.Null(store.Read<ScoreData>(KothFileStore.ScoresFile));

            var bad = BadFiles();
            Assert.Single(bad);
            Assert.Equal(Path.GetFileName(bad[0]), reportedBackup);
            Assert.Equal("this is not xml", File.ReadAllText(bad[0]));
        }
    }
}
