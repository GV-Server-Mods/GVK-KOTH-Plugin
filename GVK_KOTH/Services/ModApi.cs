using System;
using System.Collections.Generic;
using Sandbox.ModAPI;

namespace GVK.Koth.Services
{
    /// <summary>
    /// API for server-side mods (e.g. SiegeableSafeZones), over mod messages like the WeaponCore API: a mod sends
    /// "ApiEndpointRequest" on <see cref="Channel"/> and gets back a Dictionary&lt;string, Delegate&gt;.
    /// Mod messages never leave the server, so clients can't call it. See docs/API.md.
    /// </summary>
    public sealed class ModApi
    {
        /// <summary>Mod message channel (from GV-Server-Mods/GVK-Settings#741).</summary>
        public const long Channel = 74100741L;
        public const int Version = 1;
        private const string Request = "ApiEndpointRequest";

        private readonly Plugin _plugin;
        private readonly List<Action<string, bool>> _listeners = new List<Action<string, bool>>();
        private readonly Dictionary<string, Delegate> _endpoints;

        public ModApi(Plugin plugin)
        {
            _plugin = plugin;
            _endpoints = new Dictionary<string, Delegate>
            {
                ["Version"] = new Func<int>(() => Version),
                ["AddPoints"] = new Func<long, int, string, string, int>(AddPoints),
                ["IsZoneActive"] = new Func<string, bool>(IsZoneActive),
                ["AddZoneListener"] = new Action<Action<string, bool>>(AddZoneListener),
            };
        }

        public void Register()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(Channel, Handle);
            // Mods that asked before the plugin was ready get the endpoints now.
            MyAPIGateway.Utilities.SendModMessage(Channel, _endpoints);
        }

        public void Unregister()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(Channel, Handle);
            _listeners.Clear();
        }

        private void Handle(object message)
        {
            if (message is string text && text == Request)
                MyAPIGateway.Utilities.SendModMessage(Channel, _endpoints);
        }

        /// <summary>
        /// Adds (or with a negative change, removes) season points for a faction. Points only, no SC; the total
        /// never goes below 0. Announced in chat and on Discord. Returns the new total, or -1 if the faction or the
        /// plugin isn't available.
        /// </summary>
        private int AddPoints(long factionId, int change, string source, string reason)
        {
            var data = _plugin.Data;
            var faction = MyAPIGateway.Session?.Factions?.TryGetFactionById(factionId);
            if (data == null || faction == null || change == 0)
                return faction == null || data == null ? -1 : data.Scores.Get(factionId)?.Points ?? 0;

            string why = string.IsNullOrWhiteSpace(reason) ? "no reason given" : reason.Trim();
            string from = string.IsNullOrWhiteSpace(source) ? "API" : "API: " + source.Trim();
            var entry = data.AddPoints(faction.FactionId, faction.Tag, faction.Name, change, from, why);

            _plugin.Announce.Both($"[{faction.Tag}] {(entry.Change >= 0 ? "gained" : "lost")} {Math.Abs(entry.Change)} KOTH points: {why}. Total {entry.NewTotal}.");
            return entry.NewTotal;
        }

        /// <summary>True while any zone with this profile name is open.</summary>
        private bool IsZoneActive(string zoneName)
        {
            return _plugin.Zones?.IsProfileActive(zoneName) == true;
        }

        private void AddZoneListener(Action<string, bool> listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Add(listener);
        }

        /// <summary>Tells listening mods that a zone opened (true) or closed (false).</summary>
        public void ZoneChanged(string zoneName, bool active)
        {
            foreach (var listener in _listeners.ToArray())
            {
                try
                {
                    listener(zoneName, active);
                }
                catch (Exception ex)
                {
                    Plugin.Log.Warn(ex, "GVK KOTH: a mod's zone listener threw; removing it.");
                    _listeners.Remove(listener);
                }
            }
        }
    }
}
