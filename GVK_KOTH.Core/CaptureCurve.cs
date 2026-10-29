namespace GVK.Koth.Core
{
    /// <summary>
    /// Capture speed for a number of players, from the King of the Hill mod (ZoneBlock.GetProgress):
    /// 0 = 0x, 1 = 1x, 2 = 1.27x, 3 = 1.42x, 5 = 1.59x, levelling off below 2x.
    /// </summary>
    public static class CaptureCurve
    {
        public static double Speed(int players)
        {
            if (players <= 0)
                return 0;

            double n = players;
            return (n * n - 1) / (n * n + 3 * n + 1) + 1;
        }
    }
}
