using System;
using System.Reflection;
using System.Threading.Tasks;

namespace GVK.Koth.Services
{
    /// <summary>
    /// Posts to a faction's Discord channel through the SEDiscordBridge plugin (SEDB), if it is installed and the
    /// faction's Name has a FactionChannels entry. SEDB only forwards faction chat written by players, so a server
    /// message has to call its bridge directly. Found by reflection, so GVK KOTH runs without SEDB.
    /// </summary>
    internal static class SedbBridge
    {
        private static bool _searched;
        private static PropertyInfo _plugin;
        private static FieldInfo _bridge;
        private static MethodInfo _send;

        public static void PostFaction(string factionName, string message)
        {
            try
            {
                if (!_searched)
                    Find();
                if (_send == null || string.IsNullOrEmpty(factionName))
                    return;

                var plugin = _plugin.GetValue(null);
                var bridge = plugin == null ? null : _bridge.GetValue(plugin);
                if (bridge == null)
                    return;

                // SendFacChatMessage blocks on the Discord call; keep it off the game thread. It logs its own errors.
                Task.Run(() =>
                {
                    try
                    {
                        _send.Invoke(bridge, new object[] { "KOTH", message, factionName });
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.Warn(ex, "GVK KOTH: SEDB faction message failed.");
                    }
                });
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn(ex, "GVK KOTH: SEDB faction message failed.");
            }
        }

        private static void Find()
        {
            _searched = true;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("SEDiscordBridge.SEDiscordBridgePlugin", false);
                if (type == null)
                    continue;

                _plugin = type.GetProperty("Static", BindingFlags.Public | BindingFlags.Static);
                _bridge = type.GetField("DDBridge", BindingFlags.Public | BindingFlags.Instance);
                var send = _bridge?.FieldType.GetMethod("SendFacChatMessage", new[] { typeof(string), typeof(string), typeof(string) });
                _send = _plugin != null ? send : null;
                if (_send == null)
                    Plugin.Log.Warn("GVK KOTH: SEDB found but its faction message method wasn't; faction Discord messages are off.");
                return;
            }
        }
    }
}
