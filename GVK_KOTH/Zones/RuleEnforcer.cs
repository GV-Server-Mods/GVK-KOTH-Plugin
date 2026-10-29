using System;
using System.Collections.Generic;
using System.Globalization;
using GVK.Koth.Core.Config;
using GVK.Koth.Services;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRageMath;

namespace GVK.Koth.Zones
{
    /// <summary>
    /// KOTH rules around each zone, replacing the GVK_Settings per-block scripts: player thrusters, large-grid
    /// power, and player safe zones and projectors are switched off within each zone's ranges. Fixed zones apply
    /// their rules all the time; NPC zones while their grid is here and the zone hasn't ended. Game thread only.
    /// </summary>
    public sealed class RuleEnforcer
    {
        private static readonly TimeSpan NoticeInterval = TimeSpan.FromSeconds(60);

        private readonly Announcer _announce;
        private readonly Dictionary<string, DateTime> _lastNotice = new Dictionary<string, DateTime>();

        public RuleEnforcer(Announcer announce)
        {
            _announce = announce;
        }

        public void Apply(IEnumerable<ZoneRuntime> zones, DateTime now)
        {
            foreach (var zone in zones)
            {
                var profile = zone.Profile;
                if (!profile.Enabled || (zone.IsNpc && (zone.Npc.Ended || zone.GridGone)))
                    continue;

                double range = Math.Max(profile.NoThrusterRange, Math.Max(profile.NoLargeGridPowerRange, profile.NoSafeZoneRange));
                if (range <= 0)
                    continue;

                var center = zone.Center;
                var sphere = new BoundingSphereD(center, range);
                foreach (var entity in MyEntities.GetTopMostEntitiesInSphere(ref sphere))
                {
                    if (entity is MyCubeGrid grid && !grid.Closed && grid.Physics != null && !IsNpcGrid(grid))
                        ApplyToGrid(zone, grid, center, now);
                }
            }
        }

        private void ApplyToGrid(ZoneRuntime zone, MyCubeGrid grid, Vector3D center, DateTime now)
        {
            var profile = zone.Profile;
            double thrusterSq = profile.NoThrusterRange * profile.NoThrusterRange;
            double powerSq = grid.GridSizeEnum == VRage.Game.MyCubeSize.Large ? profile.NoLargeGridPowerRange * profile.NoLargeGridPowerRange : 0;
            double safeZoneSq = profile.NoSafeZoneRange * profile.NoSafeZoneRange;

            foreach (var block in grid.GetFatBlocks())
            {
                if (!(block is IMyFunctionalBlock functional) || !functional.Enabled || IsNpc(functional.OwnerId))
                    continue;

                double distSq = Vector3D.DistanceSquared(functional.GetPosition(), center);
                string rule = null;
                double range = 0;

                if (thrusterSq > 0 && functional is IMyThrust && distSq <= thrusterSq)
                {
                    rule = "thrusters are";
                    range = profile.NoThrusterRange;
                }
                else if (powerSq > 0 && functional is IMyPowerProducer && distSq <= powerSq)
                {
                    rule = "large grid power is";
                    range = profile.NoLargeGridPowerRange;
                }
                else if (safeZoneSq > 0 && (functional is IMySafeZoneBlock || functional is IMyProjector) && distSq <= safeZoneSq)
                {
                    rule = "safe zones and projectors are";
                    range = profile.NoSafeZoneRange;
                }

                if (rule == null)
                    continue;

                functional.Enabled = false;
                Notify(functional.OwnerId, zone.Name, rule, range, now);
            }
        }

        /// <summary>Tells the block's owner why it switched off, at most once a minute per zone and rule.</summary>
        private void Notify(long ownerId, string zoneName, string rule, double range, DateTime now)
        {
            ulong steamId = MyAPIGateway.Players.TryGetSteamId(ownerId);
            if (steamId == 0)
                return;

            string key = $"{ownerId}|{zoneName}|{rule}";
            if (_lastNotice.TryGetValue(key, out var last) && now - last < NoticeInterval)
                return;
            _lastNotice[key] = now;

            string km = (range / 1000).ToString("0.#", CultureInfo.InvariantCulture);
            _announce.Chat($"KOTH rule: {rule} switched off within {km} km of {zoneName}.", steamId);
        }

        private static bool IsNpcGrid(MyCubeGrid grid)
        {
            return grid.BigOwners.Count > 0 && IsNpc(grid.BigOwners[0]);
        }

        private static bool IsNpc(long identityId)
        {
            return identityId != 0 && MySession.Static?.Players?.IdentityIsNpc(identityId) == true;
        }
    }
}
