using System;
using System.Linq;
using System.Text;
using GVK.Koth.Core.Config;
using GVK.Koth.Zones;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace GVK.Koth.Commands
{
    /// <summary>!koth: every zone, whether it's open, when it next runs, and its GPS.</summary>
    public class KothRootCommand : CommandModule
    {
        [Command("koth", "Lists KOTH zones, which are open, when they next run, and their GPS.")]
        [Permission(MyPromoteLevel.None)]
        public void Zones()
        {
            var zones = Plugin.Instance?.Zones;
            if (zones == null)
            {
                Context.Respond("KOTH is not running.");
                return;
            }

            var now = DateTime.UtcNow;
            var sb = new StringBuilder();
            sb.AppendLine("KOTH zones (!koth score for standings):");
            foreach (var zone in zones.Zones.Where(z => z.Profile.Enabled && z.Profile.Mode != ZoneMode.RulesOnly
                                                         && z.Profile.Attach == ZoneAttach.Coordinates))
            {
                sb.AppendLine($"{zone.Name}: {zones.NextRunText(zone, now)}");
                sb.AppendLine("  " + LootDelivery.Gps("KOTH " + zone.Name, zone.Center));
            }
            Context.Respond(sb.ToString());
        }
    }

    [Category("koth")]
    public class KothPlayerCommands : CommandModule
    {
        [Command("score", "Shows the season KOTH standings.")]
        [Permission(MyPromoteLevel.None)]
        public void Score()
        {
            var zones = Plugin.Instance?.Zones;
            if (zones == null)
            {
                Context.Respond("KOTH is not running.");
                return;
            }
            Context.Respond(zones.StandingsText("Season KOTH standings", includeGain: false).Replace("**", ""));
        }
    }
}
