using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace GVK.Koth.Core
{
    /// <summary>
    /// Reads and writes the plugin's XML data files (season totals and saved state).
    /// Files are always UTF-8 and say so in their XML declaration. A file that can't be read is copied to
    /// "&lt;name&gt;.bad-&lt;time&gt;" for the admin and the plugin starts empty instead of overwriting it.
    /// </summary>
    public sealed class KothFileStore
    {
        public const string ScoresFile = "Scores.xml";
        public const string StateFile = "State.xml";

        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        private readonly string _folder;
        private readonly Action<string, string, Exception> _onUnreadable;

        /// <param name="folder">The data folder; created if missing.</param>
        /// <param name="onUnreadable">Called with (file name, backup file name, error) when a file was set aside.</param>
        public KothFileStore(string folder, Action<string, string, Exception> onUnreadable = null)
        {
            _folder = folder;
            _onUnreadable = onUnreadable;
            Directory.CreateDirectory(folder);
        }

        public string Folder => _folder;

        /// <summary>Serializes to UTF-8 bytes. Run it on the game thread (it snapshots the data), then <see cref="Write"/> anywhere.</summary>
        public static byte[] Serialize<T>(T data)
        {
            using (var stream = new MemoryStream())
            {
                var settings = new XmlWriterSettings { Encoding = Utf8, Indent = true };
                using (var writer = XmlWriter.Create(stream, settings))
                    new XmlSerializer(typeof(T)).Serialize(writer, data);
                return stream.ToArray();
            }
        }

        /// <summary>Writes to a temp file first, then swaps it in, so a crash mid-write can't leave half a file.</summary>
        public void Write(string file, byte[] content)
        {
            string path = Path.Combine(_folder, file);
            string temp = path + ".tmp";
            File.WriteAllBytes(temp, content);
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
        }

        /// <summary>
        /// Reads a file, or returns null when it is missing or unreadable (then the file is set aside as .bad-*).
        /// Accepts UTF-8, UTF-8 with BOM and UTF-16 with BOM, and files written by older builds that were
        /// UTF-8 but declared utf-16 (the older builds).
        /// </summary>
        public T Read<T>(string file) where T : class
        {
            string path = Path.Combine(_folder, file);
            if (!File.Exists(path))
                return null;

            try
            {
                // A TextReader makes the serializer ignore the declared encoding, so a wrong declaration can't break the read.
                using (var reader = new StreamReader(path, Utf8, true))
                    return (T)new XmlSerializer(typeof(T)).Deserialize(reader);
            }
            catch (Exception ex)
            {
                string backup = path + ".bad-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                File.Copy(path, backup, true);
                _onUnreadable?.Invoke(file, Path.GetFileName(backup), ex);
                return null;
            }
        }
    }
}
