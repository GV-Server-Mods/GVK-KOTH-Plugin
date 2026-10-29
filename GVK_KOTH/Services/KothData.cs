using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GVK.Koth.Core;

namespace GVK.Koth.Services
{
    /// <summary>
    /// Season totals, the CSV score log and saved state, all in the plugin's data folder.
    /// Reads happen at load; writes are queued to one background thread so the game thread never waits on disk.
    /// </summary>
    public sealed class KothData : IDisposable
    {
        private const string ScoresFile = "Scores.xml";
        private const string StateFile = "State.xml";
        private const string LogFile = "ScoreLog.csv";

        private readonly string _folder;
        private readonly BlockingCollection<Action> _writes = new BlockingCollection<Action>();
        private readonly Task _writer;

        public KothData(string folder)
        {
            _folder = folder;
            Directory.CreateDirectory(folder);

            Scores = new ScoreBook(Read<ScoreData>(ScoresFile));
            State = Read<KothState>(StateFile) ?? new KothState();

            _writer = Task.Factory.StartNew(WriteLoop, TaskCreationOptions.LongRunning);
        }

        public ScoreBook Scores { get; }
        public KothState State { get; }

        /// <summary>Applies a point change, logs it and saves the totals. Game thread only.</summary>
        public ScoreLogEntry AddPoints(long factionId, string tag, string name, int change, string source, string reason)
        {
            var entry = Scores.Add(factionId, tag, name, change, source, reason, DateTime.UtcNow);
            string line = entry.ToCsv();
            Queue(() => AppendLog(line));
            SaveScores();
            return entry;
        }

        public void SaveScores()
        {
            string xml = Serialize(Scores.Data);
            Queue(() => File.WriteAllText(Path.Combine(_folder, ScoresFile), xml));
        }

        public void SaveState()
        {
            string xml = Serialize(State);
            Queue(() => File.WriteAllText(Path.Combine(_folder, StateFile), xml));
        }

        private void AppendLog(string line)
        {
            string path = Path.Combine(_folder, LogFile);
            if (!File.Exists(path))
                File.WriteAllText(path, ScoreLogEntry.CsvHeader + Environment.NewLine);
            File.AppendAllText(path, line + Environment.NewLine);
        }

        private void Queue(Action write)
        {
            if (!_writes.IsAddingCompleted)
                _writes.Add(write);
        }

        private void WriteLoop()
        {
            foreach (var write in _writes.GetConsumingEnumerable())
            {
                try
                {
                    write();
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "GVK KOTH: failed to write data file.");
                }
            }
        }

        private T Read<T>(string file) where T : class
        {
            string path = Path.Combine(_folder, file);
            if (!File.Exists(path))
                return null;

            try
            {
                using (var reader = File.OpenRead(path))
                    return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
            }
            catch (Exception ex)
            {
                // Keep the bad file for the admin to inspect instead of overwriting it.
                string backup = path + ".bad-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                File.Copy(path, backup, true);
                Plugin.Log.Error(ex, $"GVK KOTH: couldn't read {path}; copied it to {backup} and started empty.");
                Plugin.Instance?.Alerts?.Send($"Couldn't read {file}; copied it to {Path.GetFileName(backup)} and started empty.");
                return null;
            }
        }

        private static string Serialize<T>(T data)
        {
            using (var writer = new StringWriter())
            {
                new XmlSerializer(typeof(T)).Serialize(writer, data);
                return writer.ToString();
            }
        }

        /// <summary>Finishes queued writes. Call on session unload.</summary>
        public void Dispose()
        {
            _writes.CompleteAdding();
            if (!_writer.Wait(TimeSpan.FromSeconds(10)))
                Plugin.Log.Warn("GVK KOTH: data writes didn't finish within 10 seconds.");
        }
    }
}
