using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using GVK.Koth.Core;

namespace GVK.Koth.Services
{
    /// <summary>
    /// Season totals, the CSV score log and saved state, all in the plugin's data folder.
    /// Reads happen at load; writes are queued to one background thread so the game thread never waits on disk.
    /// </summary>
    public sealed class KothData : IDisposable
    {
        private const string LogFile = "ScoreLog.csv";

        private readonly string _folder;
        private readonly KothFileStore _store;
        private readonly BlockingCollection<Action> _writes = new BlockingCollection<Action>();
        private readonly Task _writer;

        public KothData(string folder)
        {
            _folder = folder;
            _store = new KothFileStore(folder, OnUnreadable);

            Scores = new ScoreBook(_store.Read<ScoreData>(KothFileStore.ScoresFile));
            State = _store.Read<KothState>(KothFileStore.StateFile) ?? new KothState();

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
            byte[] xml = KothFileStore.Serialize(Scores.Data);
            Queue(() => _store.Write(KothFileStore.ScoresFile, xml));
        }

        public void SaveState()
        {
            byte[] xml = KothFileStore.Serialize(State);
            Queue(() => _store.Write(KothFileStore.StateFile, xml));
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

        private void OnUnreadable(string file, string backup, Exception ex)
        {
            Plugin.Log.Error(ex, $"GVK KOTH: couldn't read {file}; copied it to {backup} and started empty.");
            Plugin.Instance?.Alerts?.Send($"Couldn't read {file}; copied it to {backup} and started empty.");
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
