using System.Text;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace GVK.Koth.Commands
{
    [Category("koth")]
    public class KothAdminCommands : CommandModule
    {
        private static Plugin Plugin => Plugin.Instance;

        [Command("reload", "Reloads the GVK KOTH config and lists any problems in it.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Reload()
        {
            if (Plugin == null)
            {
                Context.Respond("GVK KOTH is not loaded.");
                return;
            }

            Plugin.LoadConfig();
            var problems = Plugin.ConfigProblems;
            if (problems.Count == 0)
            {
                Context.Respond($"GVK KOTH config reloaded: {Plugin.Config.Zones.Count} zones, {Plugin.Config.Slots.Count} slots, no problems.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"GVK KOTH config reloaded with {problems.Count} problem(s):");
            foreach (string problem in problems)
                sb.AppendLine("- " + problem);
            Context.Respond(sb.ToString());
        }

        [Command("status", "Shows GVK KOTH zones and their state.")]
        [Permission(MyPromoteLevel.Admin)]
        public void Status()
        {
            if (Plugin?.Config == null)
            {
                Context.Respond("GVK KOTH is not loaded.");
                return;
            }

            var cfg = Plugin.Config;
            var sb = new StringBuilder();
            sb.AppendLine($"GVK KOTH: {(cfg.Enabled ? "enabled" : "DISABLED")}, session {(Plugin.SessionLoaded ? "loaded" : "not loaded")}");
            sb.AppendLine($"Zones: {cfg.Zones.Count}, slots: {cfg.Slots.Count}, config problems: {Plugin.ConfigProblems.Count}");
            foreach (var zone in cfg.Zones)
                sb.AppendLine($"- {zone.Name}: {zone.Mode}, {zone.Attach}, {zone.Activation}{(zone.Enabled ? "" : " (disabled)")}");
            Context.Respond(sb.ToString());
        }
    }
}
