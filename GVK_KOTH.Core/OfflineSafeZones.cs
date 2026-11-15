using System;

namespace GVK.Koth.Core
{
    /// <summary>
    /// Reads the names of the offline safe zones the DynamicSafeZones mod (Workshop 1502020913) makes:
    /// "(FSZ)_TAG_gridEntityId" ("(DSZ)" is an older prefix the mod still clears up). They are safe-zone entities,
    /// not blocks.
    /// </summary>
    public static class OfflineSafeZones
    {
        public static bool IsOfflineZone(string displayName)
        {
            return displayName != null
                && (displayName.StartsWith("(FSZ)", StringComparison.Ordinal) || displayName.StartsWith("(DSZ)", StringComparison.Ordinal));
        }

        /// <summary>The entity id of the grid the zone covers (the last "_" part), or 0 for a temporary zone or a bad name.</summary>
        public static long GridId(string displayName)
        {
            if (!IsOfflineZone(displayName))
                return 0;

            int split = displayName.LastIndexOf('_');
            return split >= 0 && long.TryParse(displayName.Substring(split + 1), out long id) ? id : 0;
        }
    }
}
