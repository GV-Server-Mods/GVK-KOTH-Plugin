using System.Text.RegularExpressions;

namespace GVK.Koth.Core
{
    /// <summary>
    /// Reads the KOTH tag that MES copies from a behavior profile into the remote control's CustomData,
    /// e.g. "GVK-KOTH=WreckShort;".
    /// </summary>
    public static class NpcTag
    {
        private static readonly Regex Pattern = new Regex(@"GVK-KOTH\s*=\s*([^;\r\n]+)\s*;?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex LinkPattern = new Regex(@"\[GVK-KOTH-Link(?:\s*:\s*([^\]]*))?\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Reads a linked-block tag: "[GVK-KOTH-Link]" (follows any zone within that zone's LinkRange, zone = null)
        /// or "[GVK-KOTH-Link:KharToba]" (follows that zone only).
        /// </summary>
        public static bool ReadLink(string customData, out string zone)
        {
            zone = null;
            if (string.IsNullOrEmpty(customData) || customData.IndexOf("GVK-KOTH-Link", System.StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            var match = LinkPattern.Match(customData);
            if (!match.Success)
                return false;

            string name = match.Groups[1].Success ? match.Groups[1].Value.Trim() : "";
            zone = name.Length == 0 ? null : name;
            return true;
        }

        /// <summary>The profile name in the tag, or null if there is none.</summary>
        public static string Read(string customData)
        {
            if (string.IsNullOrEmpty(customData) || customData.IndexOf("GVK-KOTH", System.StringComparison.OrdinalIgnoreCase) < 0)
                return null;

            var match = Pattern.Match(customData);
            if (!match.Success)
                return null;

            string name = match.Groups[1].Value.Trim();
            return name.Length == 0 ? null : name;
        }
    }
}
