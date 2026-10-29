using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;
using GVK.Koth.Core.Config;
using GVK.Koth.Services;
using GVK.Koth.Zones;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.API.Session;

namespace GVK.Koth
{
    /// <summary>
    /// GVK KOTH plugin: lifecycle, config, and the per-tick hook that drives the zones.
    /// </summary>
    public class Plugin : TorchPluginBase, IWpfPlugin
    {
        public static readonly Logger Log = LogManager.GetCurrentClassLogger();

        private const string ConfigFileName = "GVK_KOTH.cfg";

        // Longest gap counted between checks, so a hitch or a paused server doesn't jump a bar forward.
        private const double MaxStepSeconds = 5;

        private readonly Stopwatch _clock = new Stopwatch();
        private KothConfig _config;
        private UserControl _control;
        private int _tick;

        public static Plugin Instance { get; private set; }

        public KothConfig Config => _config;

        /// <summary>Problems found the last time the config was loaded.</summary>
        public IReadOnlyList<string> ConfigProblems { get; private set; } = Array.Empty<string>();

        /// <summary>Folder for scores, the score log and saved state. Delete it (server stopped) for a new season.</summary>
        public string DataPath => Path.Combine(StoragePath, "GVK_KOTH");

        public AdminAlerts Alerts { get; private set; }
        public Announcer Announce { get; private set; }
        public KothData Data { get; private set; }
        public ZoneManager Zones { get; private set; }
        public ModApi Api { get; private set; }
        public MesBridge Mes { get; private set; }
        public ClientSync Sync { get; private set; }

        public bool SessionLoaded => Zones != null;

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;

            Alerts = new AdminAlerts(this);
            Announce = new Announcer(this);
            LoadConfig();

            if (Torch.Managers.GetManager(typeof(ITorchSessionManager)) is ITorchSessionManager sessionManager)
                sessionManager.SessionStateChanged += OnSessionStateChanged;
            else
                Log.Error("Torch session manager not found; GVK KOTH won't start.");
        }

        private void OnSessionStateChanged(ITorchSession session, TorchSessionState newState)
        {
            if (newState == TorchSessionState.Loaded)
                StartSession();
            else if (newState == TorchSessionState.Unloading)
                StopSession();
        }

        private void StartSession()
        {
            try
            {
                Data = new KothData(DataPath);
                Mes = new MesBridge();
                Sync = new ClientSync();
                var zones = new ZoneManager(this, Data, Announce);
                zones.Rebuild();
                zones.Start();
                Zones = zones;
                Api = new ModApi(this);
                Api.Register();
                _clock.Restart();
                Log.Info($"GVK KOTH started with {Config.Zones.Count} zones and {Config.Slots.Count} slots.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GVK KOTH failed to start.");
                Alerts.Send("Failed to start; see the Torch log.");
            }
        }

        private void StopSession()
        {
            try
            {
                Api?.Unregister();
                Zones?.Stop();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GVK KOTH: failed to save state on unload.");
            }

            Api = null;
            Zones = null;
            Sync = null;
            Mes = null;
            Data?.Dispose();
            Data = null;
        }

        public override void Update()
        {
            var zones = Zones;
            if (zones == null || Config == null || !Config.Enabled)
                return;

            if (++_tick < Math.Max(1, Config.UpdateIntervalTicks))
                return;
            _tick = 0;

            // Real elapsed time, so capture times hold even when sim speed drops.
            double dt = Math.Min(_clock.Elapsed.TotalSeconds, MaxStepSeconds);
            _clock.Restart();

            try
            {
                zones.Tick(dt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GVK KOTH: zone update failed.");
            }
        }

        /// <summary>
        /// Reads GVK_KOTH.cfg (writing the commented sample there first if it's missing), checks it and reports
        /// every problem. The plugin only reads the file; it never saves over your edits. If the file can't be read,
        /// the previous config stays in use.
        /// </summary>
        public void LoadConfig()
        {
            string path = Path.Combine(StoragePath, ConfigFileName);
            if (!File.Exists(path))
                WriteDefaultConfig(path);

            KothConfig loaded = null;
            try
            {
                using (var reader = File.OpenRead(path))
                    loaded = (KothConfig)new XmlSerializer(typeof(KothConfig)).Deserialize(reader);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"GVK KOTH: couldn't read {path}.");
                Alerts.Send($"couldn't read {ConfigFileName} ({ex.GetBaseException().Message}); {(_config == null ? "running with no zones" : "keeping the previous config")}.");
            }

            if (loaded != null)
            {
                KothConfig.ApplyDefaults(loaded);
                _config = loaded;
            }
            else if (_config == null)
            {
                _config = new KothConfig();
                KothConfig.ApplyDefaults(_config);
            }

            ConfigProblems = ConfigCheck.Run(_config);
            foreach (string problem in ConfigProblems)
                Log.Warn("GVK KOTH config: " + problem);
            if (ConfigProblems.Count > 0)
                Alerts.Send($"config has {ConfigProblems.Count} problem(s):\n- " + string.Join("\n- ", ConfigProblems));

            Zones?.Rebuild();
        }

        /// <summary>Writes the commented sample config (embedded from docs/GVK_KOTH.sample.cfg).</summary>
        private static void WriteDefaultConfig(string path)
        {
            try
            {
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GVK_KOTH.sample.cfg"))
                using (var file = File.Create(path))
                    stream?.CopyTo(file);
                Log.Info($"GVK KOTH: wrote the sample config to {path}.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"GVK KOTH: couldn't write the sample config to {path}.");
            }
        }

        public UserControl GetControl()
        {
            return _control ??= new UserControl
            {
                Content = new TextBlock
                {
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap,
                    Text = $"Edit {ConfigFileName} in the Torch Instance folder, then run !koth reload in game. A settings page is planned."
                }
            };
        }

        public override void Dispose()
        {
            if (Torch?.Managers?.GetManager(typeof(ITorchSessionManager)) is ITorchSessionManager sessionManager)
                sessionManager.SessionStateChanged -= OnSessionStateChanged;

            StopSession();
            Instance = null;
            base.Dispose();
        }
    }
}
