using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using GVK.Koth.Core.Config;
using GVK.Koth.Services;
using GVK.Koth.Zones;
using NLog;
using Torch;
using Torch.API;
using Torch.API.Plugins;
using Torch.API.Session;
using Torch.Views;

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
        private Persistent<KothConfig> _config;
        private PropertyGrid _control;
        private int _tick;

        public static Plugin Instance { get; private set; }

        public KothConfig Config => _config?.Data;

        /// <summary>Problems found the last time the config was loaded.</summary>
        public IReadOnlyList<string> ConfigProblems { get; private set; } = Array.Empty<string>();

        /// <summary>Folder for scores, the score log and saved state. Delete it (server stopped) for a new season.</summary>
        public string DataPath => Path.Combine(StoragePath, "GVK_KOTH");

        public AdminAlerts Alerts { get; private set; }
        public Announcer Announce { get; private set; }
        public KothData Data { get; private set; }
        public ZoneManager Zones { get; private set; }

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
                var zones = new ZoneManager(this, Data, Announce);
                zones.Rebuild();
                zones.Start();
                Zones = zones;
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
                Zones?.Stop();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "GVK KOTH: failed to save state on unload.");
            }

            Zones = null;
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
        /// Loads the config (creating a default one), checks it and reports every problem.
        /// </summary>
        public void LoadConfig()
        {
            string path = Path.Combine(StoragePath, ConfigFileName);
            try
            {
                _config = Persistent<KothConfig>.Load(path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"GVK KOTH: couldn't read {path}; using defaults until it's fixed.");
                _config = null;
            }

            if (_config?.Data == null)
            {
                _config = new Persistent<KothConfig>(path, new KothConfig());
                if (!File.Exists(path))
                    _config.Save();
            }

            ConfigProblems = ConfigCheck.Run(_config.Data);
            foreach (string problem in ConfigProblems)
                Log.Warn("GVK KOTH config: " + problem);
            if (ConfigProblems.Count > 0)
                Alerts.Send($"config has {ConfigProblems.Count} problem(s):\n- " + string.Join("\n- ", ConfigProblems));

            Zones?.Rebuild();

            if (_control != null)
                _control.Dispatcher.Invoke(() => _control.DataContext = Config);
        }

        public UserControl GetControl()
        {
            return _control ??= new PropertyGrid { DataContext = Config };
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
