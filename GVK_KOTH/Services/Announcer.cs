using System;
using Torch.API.Managers;
using VRageMath;

namespace GVK.Koth.Services
{
    /// <summary>
    /// Player-facing messages: in-game chat as "KOTH", and the public Discord webhook.
    /// </summary>
    public sealed class Announcer
    {
        private static readonly Color ChatColor = new Color(255, 196, 0);

        private readonly Plugin _plugin;

        public Announcer(Plugin plugin)
        {
            _plugin = plugin;
        }

        /// <summary>Chat to everyone (or one player) in game.</summary>
        public void Chat(string message, ulong steamId = 0)
        {
            try
            {
                var chat = _plugin.Torch.CurrentSession?.Managers.GetManager<IChatManagerServer>();
                chat?.SendMessageAsOther("KOTH", message, ChatColor, steamId);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn(ex, "GVK KOTH: chat message failed.");
            }
        }

        public void Discord(string message)
        {
            Services.Discord.Post(_plugin.Config?.DiscordWebhookUrl, message);
        }

        /// <summary>Chat and Discord.</summary>
        public void Both(string message)
        {
            Chat(message);
            Discord(message);
        }
    }

    /// <summary>
    /// Problems the admin should see: logged, and posted to the admin Discord webhook if one is set.
    /// </summary>
    public sealed class AdminAlerts
    {
        private readonly Plugin _plugin;

        public AdminAlerts(Plugin plugin)
        {
            _plugin = plugin;
        }

        public void Send(string message)
        {
            Plugin.Log.Warn("GVK KOTH alert: " + message);
            Services.Discord.Post(_plugin.Config?.DiscordAdminWebhookUrl, "GVK KOTH: " + message);
        }
    }
}
