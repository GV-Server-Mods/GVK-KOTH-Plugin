using System;
using System.Collections.Generic;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace GVK.Koth.Services
{
    /// <summary>
    /// Adds KOTH GPS markers straight to players' GPS lists. A GPS written in a plugin's chat message isn't picked
    /// up by the game (only normal player chat is scanned), so markers are added directly. Each marker replaces any
    /// marker with the same name and removes itself after its lifetime. Game thread only.
    /// </summary>
    public static class GpsMarkers
    {
        public static readonly Color OpenColor = new Color(255, 128, 31);
        public static readonly Color UpcomingColor = new Color(160, 90, 30);
        public static readonly Color LootColor = new Color(255, 215, 0);

        private static readonly List<IMyPlayer> Players = new List<IMyPlayer>();
        private static readonly List<IMyGps> Existing = new List<IMyGps>();

        private static bool Enabled => Plugin.Instance?.Config?.AddGpsMarkers != false;

        /// <summary>Adds the marker for every online player, or only those within range of the position.</summary>
        public static void AddForAll(string name, string description, Vector3D position, Color color, TimeSpan lifetime, double withinMeters = 0)
        {
            if (!Enabled)
                return;

            Players.Clear();
            MyAPIGateway.Players.GetPlayers(Players);
            foreach (var player in Players)
            {
                if (player == null || player.IsBot)
                    continue;
                if (withinMeters > 0 && Vector3D.DistanceSquared(player.GetPosition(), position) > withinMeters * withinMeters)
                    continue;
                Add(player.IdentityId, name, description, position, color, lifetime);
            }
            Players.Clear();
        }

        public static void Add(long identityId, string name, string description, Vector3D position, Color color, TimeSpan lifetime)
        {
            if (!Enabled || identityId == 0)
                return;

            try
            {
                Remove(identityId, name);
                var gps = MyAPIGateway.Session.GPS.Create(name, description, position, true);
                gps.GPSColor = color;
                if (MySession.Static != null)
                    gps.DiscardAt = MySession.Static.ElapsedPlayTime + lifetime;
                MyAPIGateway.Session.GPS.AddGps(identityId, gps);
            }
            catch (Exception ex)
            {
                Plugin.Log.Warn(ex, $"GVK KOTH: couldn't add GPS '{name}'.");
            }
        }

        /// <summary>Removes the marker from every online player (e.g. when a zone closes).</summary>
        public static void RemoveForAll(string name)
        {
            if (!Enabled)
                return;

            Players.Clear();
            MyAPIGateway.Players.GetPlayers(Players);
            foreach (var player in Players)
            {
                if (player != null && !player.IsBot)
                    Remove(player.IdentityId, name);
            }
            Players.Clear();
        }

        private static void Remove(long identityId, string name)
        {
            Existing.Clear();
            MyAPIGateway.Session.GPS.GetGpsList(identityId, Existing);
            foreach (var gps in Existing)
            {
                if (string.Equals(gps.Name, name, StringComparison.Ordinal))
                    MyAPIGateway.Session.GPS.RemoveGps(identityId, gps.Hash);
            }
            Existing.Clear();
        }

        public static string ZoneName(string zone) => "KOTH " + zone;
        public static string LootName(string zone) => "KOTH " + zone + " Loot";
    }
}
