using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;
using GVK.Koth.Core.Config;
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

        private Persistent<KothConfig> _config;
        private PropertyGrid _control;
        private int _tick;

        public static Plugin Instance { get; private set; }

        public KothConfig Config => _config?.Data;

        /// <summary>Problems found the last time the config was loaded.</summary>
        public IReadOnlyList<string> ConfigProblems { get; private set; } = Array.Empty<string>();

        /// <summary>Folder for scores, the score log and saved state. Delete it (server stopped) for a new season.</summary>
        public string DataPath => Path.Combine(StoragePath, "GVK_KOTH");

        public bool SessionLoaded { get; private set; }

        public override void Init(ITorchBase torch)
        {
            base.Init(torch);
            Instance = this;

            Directory.CreateDirectory(DataPath);
            LoadConfig();

            if (Torch.Managers.GetManager(typeof(ITorchSessionManager)) is ITorchSessionManager sessionManager)
                sessionManager.SessionStateChanged += OnSessionStateChanged;
            else
                Log.Error("Torch session manager not found; GVK KOTH won't start.");
        }

        private void OnSessionStateChanged(ITorchSession session, TorchSessionState newState)
        {
            if (newState == TorchSessionState.Loaded)
            {
                SessionLoaded = true;
                Log.Info("GVK KOTH: session loaded.");
            }
            else if (newState == TorchSessionState.Unloading)
            {
                SessionLoaded = false;
            }
        }

        public override void Update()
        {
            if (!SessionLoaded || Config == null || !Config.Enabled)
                return;

            if (++_tick < Math.Max(1, Config.UpdateIntervalTicks))
                return;
            _tick = 0;
        }

        /// <summary>
        /// Loads the config (creating a default one), checks it and logs every problem.
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

            SessionLoaded = false;
            Instance = null;
            base.Dispose();
        }
    }
}
