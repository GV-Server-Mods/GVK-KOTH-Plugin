using System;
using System.Linq;
using System.Text;
using GVK.Koth.Core.Config;
using GVK.Koth.Services;
using Torch.Commands;
using Torch.Commands.Permissions;
using VRage.Game.ModAPI;

namespace GVK.Koth.Commands
{
    // Torch can't register a bare "!koth" alongside "!koth <subcommand>" (a command node can't have both), so the
    // zone list is "!koth list".
    [Category("koth")]
    public class KothPlayerCommands : CommandModule
    {
        [Command("list", "Lists KOTH zones, which are open and when they next run, and adds GPS markers for open ones.")]
        [Permission(MyPromoteLevel.None)]
        public void List()
        {
            var zones = Plugin.Instance?.Zones;
            if (zones == null)
            {
                Context.Respond("KOTH is not running.");
                return;
            }

            var now = DateTime.UtcNow;
            long identityId = Context.Player?.IdentityId ?? 0;
            int gpsAdded = 0;
            var sb = new StringBuilder();
            sb.AppendLine("KOTH zones (!koth score for standings):");

            // Fixed zones always; NPC zones only while open, since their grids move or come and go.
            foreach (var zone in zones.Zones.Where(z => z.Profile.Enabled && z.Profile.Mode != ZoneMode.RulesOnly
                                                         && (!z.IsNpc || z.Active)))
            {
                sb.AppendLine($"{zone.Name}: {zones.NextRunText(zone, now)}");
                if (zone.Active && identityId != 0)
                {
                    GpsMarkers.Add(identityId, GpsMarkers.ZoneName(zone.Name), "King of the Hill: open", zone.Center,
                        GpsMarkers.OpenColor, TimeSpan.FromHours(3));
                    gpsAdded++;
                }
            }

            if (gpsAdded > 0)
                sb.AppendLine($"GPS added for {gpsAdded} open zone(s).");
            Context.Respond(sb.ToString());
        }

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
