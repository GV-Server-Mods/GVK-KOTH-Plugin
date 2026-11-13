using System;
using System.Collections.Generic;

namespace GVK.Koth.Core.Config
{
    /// <summary>The KOTH rule ranges of a zone, shared by the rule enforcer and the tests.</summary>
    public static class RuleRanges
    {
        /// <summary>The largest rule range of the zone, so one sphere search finds every block a rule can touch. 0 = no rules.</summary>
        public static double Max(ZoneProfile profile)
        {
            return Math.Max(Math.Max(Math.Max(profile.NoThrusterRange, profile.NoLargeGridPowerRange),
                                     Math.Max(profile.NoSafeZoneRange, profile.NoProjectorRange)),
                            profile.NoDrillRange);
        }

        /// <summary>True if a ConveyorSorter block subtype is a ToolCore drill from the config list.</summary>
        public static bool IsToolCoreDrill(string subtypeId, ICollection<string> drillSubtypes)
        {
            return !string.IsNullOrEmpty(subtypeId) && drillSubtypes != null && drillSubtypes.Contains(subtypeId);
        }
    }
}
