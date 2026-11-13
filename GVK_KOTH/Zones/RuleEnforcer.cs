using System;
using System.Collections.Generic;
using System.Globalization;
using GVK.Koth.Core.Config;
using GVK.Koth.Services;
using NLog;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using SpaceEngineers.Game.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace GVK.Koth.Zones
{
    /// <summary>
    /// KOTH rules around each zone, replacing the GVK_Settings per-block scripts: player thrusters, large-grid
    /// power, player safe zones, projectors and drills are switched off within each zone's ranges, and hand drills
    /// are unequipped. Fixed zones apply their rules all the time; NPC zones while their grid is here and the zone
    /// hasn't ended. A block inside a range is watched, so switching it back on (terminal, timer, script) is undone
    /// at once, as the old scripts did. Game thread only.
    /// </summary>
    public sealed class RuleEnforcer
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly TimeSpan NoticeInterval = TimeSpan.FromSeconds(60);

        private readonly Announcer _announce;
        private readonly Dictionary<string, DateTime> _lastNotice = new Dictionary<string, DateTime>();
        private readonly List<VRage.Game.Entity.MyEntity> _entities = new List<VRage.Game.Entity.MyEntity>();
        private readonly List<IMyPlayer> _players = new List<IMyPlayer>();

        // Zones enforcing rules at the last check; the EnabledChanged handler checks against these.
        private readonly List<ZoneRuntime> _enforcing = new List<ZoneRuntime>();
        private readonly List<ZoneRuntime> _drillZones = new List<ZoneRuntime>();
        private readonly List<NoDrillZone> _drillSpheres = new List<NoDrillZone>();
        private bool _running;

        // Blocks inside a rule range -> the check that last found them there.
        private readonly Dictionary<IMyFunctionalBlock, int> _watched = new Dictionary<IMyFunctionalBlock, int>();
        private readonly List<IMyFunctionalBlock> _unwatch = new List<IMyFunctionalBlock>();
        private int _pass;

        private ICollection<string> _toolCoreDrillSource;
        private HashSet<string> _toolCoreDrills = new HashSet<string>(StringComparer.Ordinal);

        public RuleEnforcer(Announcer announce)
        {
            _announce = announce;
            DrillGuard.EquipRefused = (identity, zone) =>
                Notify(identity, zone.Name, "hand drills are", zone.Sphere.Radius, DateTime.UtcNow);
        }

        public void Apply(IEnumerable<ZoneRuntime> zones, DateTime now, ICollection<string> toolCoreDrillSubtypes)
        {
            if (!ReferenceEquals(toolCoreDrillSubtypes, _toolCoreDrillSource))
            {
                _toolCoreDrillSource = toolCoreDrillSubtypes;
                _toolCoreDrills = new HashSet<string>(toolCoreDrillSubtypes ?? new string[0], StringComparer.Ordinal);
            }

            _running = true;
            _pass++;
            _enforcing.Clear();
            _drillZones.Clear();
            _drillSpheres.Clear();

            foreach (var zone in zones)
            {
                var profile = zone.Profile;
                if (!profile.Enabled || (zone.IsNpc && (zone.Npc.Ended || zone.GridGone)))
                    continue;

                double range = RuleRanges.Max(profile);
                if (range <= 0)
                    continue;

                var center = zone.Center;
                _enforcing.Add(zone);
                if (profile.NoDrillRange > 0)
                {
                    _drillZones.Add(zone);
                    _drillSpheres.Add(new NoDrillZone { Sphere = new BoundingSphereD(center, profile.NoDrillRange), Name = zone.Name });
                }

                var sphere = new BoundingSphereD(center, range);
                _entities.Clear();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, _entities);
                foreach (var entity in _entities)
                {
                    if (entity is MyCubeGrid grid && !grid.Closed && grid.Physics != null && !IsNpcGrid(grid))
                        ApplyToGrid(zone, grid, center, now);
                }
                _entities.Clear();
            }

            DrillGuard.SetZones(_drillSpheres.ToArray());
            ApplyToHandDrills(now);
            UnwatchMissed();
        }

        /// <summary>
        /// Stops watching blocks and switches the drill guard off: on session unload, on !koth reload (the next check
        /// starts again from the new config), and while the plugin's master switch is off. Cheap when already stopped.
        /// </summary>
        public void Stop()
        {
            if (!_running)
                return;
            _running = false;

            foreach (var block in _watched.Keys)
                Unsubscribe(block);
            _watched.Clear();
            _enforcing.Clear();
            _drillZones.Clear();
            DrillGuard.SetZones(null);
        }

        private void ApplyToGrid(ZoneRuntime zone, MyCubeGrid grid, Vector3D center, DateTime now)
        {
            bool large = grid.GridSizeEnum == MyCubeSize.Large;
            foreach (var block in grid.GetFatBlocks())
            {
                if (!(block is IMyFunctionalBlock functional) || IsNpc(functional.OwnerId))
                    continue;

                string rule = RuleFor(zone.Profile, functional, large, center, out double range);
                if (rule == null)
                    continue;

                Watch(functional);
                if (!functional.Enabled)
                    continue;

                functional.Enabled = false;
                Notify(functional.OwnerId, zone.Name, rule, range, now);
            }
        }

        /// <summary>The rule that switches this block off near this zone, or null.</summary>
        private string RuleFor(ZoneProfile profile, IMyFunctionalBlock block, bool largeGrid, Vector3D center, out double range)
        {
            range = 0;
            double distSq = Vector3D.DistanceSquared(block.GetPosition(), center);

            if (profile.NoThrusterRange > 0 && block is IMyThrust && distSq <= profile.NoThrusterRange * profile.NoThrusterRange)
            {
                range = profile.NoThrusterRange;
                return "thrusters are";
            }
            if (largeGrid && profile.NoLargeGridPowerRange > 0 && block is IMyPowerProducer && distSq <= profile.NoLargeGridPowerRange * profile.NoLargeGridPowerRange)
            {
                range = profile.NoLargeGridPowerRange;
                return "large grid power is";
            }
            if (profile.NoSafeZoneRange > 0 && block is IMySafeZoneBlock && distSq <= profile.NoSafeZoneRange * profile.NoSafeZoneRange)
            {
                range = profile.NoSafeZoneRange;
                return "safe zones are";
            }
            if (profile.NoProjectorRange > 0 && block is IMyProjector && distSq <= profile.NoProjectorRange * profile.NoProjectorRange)
            {
                range = profile.NoProjectorRange;
                return "projectors are";
            }
            if (profile.NoDrillRange > 0 && IsShipDrill(block) && distSq <= profile.NoDrillRange * profile.NoDrillRange)
            {
                range = profile.NoDrillRange;
                return "drills are";
            }
            return null;
        }

        private bool IsShipDrill(IMyFunctionalBlock block)
        {
            return block is IMyShipDrill
                || (block is IMyConveyorSorter && RuleRanges.IsToolCoreDrill(block.BlockDefinition.SubtypeName, _toolCoreDrills));
        }

        /// <summary>Unequips player hand drills inside a zone's NoDrillRange; DrillGuard stops them digging in between.</summary>
        private void ApplyToHandDrills(DateTime now)
        {
            if (_drillZones.Count == 0)
                return;

            _players.Clear();
            MyAPIGateway.Players.GetPlayers(_players);
            foreach (var player in _players)
            {
                var character = player.Character;
                if (character == null || character.IsDead || !(character.EquippedTool is IMyHandDrill) || IsNpc(player.IdentityId))
                    continue;

                var position = character.GetPosition();
                foreach (var zone in _drillZones)
                {
                    double range = zone.Profile.NoDrillRange;
                    if (Vector3D.DistanceSquared(position, zone.Center) > range * range)
                        continue;

                    (character as MyCharacter)?.SwitchToWeapon((MyDefinitionId?)null);
                    Notify(player.IdentityId, zone.Name, "hand drills are", range, now);
                    break;
                }
            }
            _players.Clear();
        }

        private void Watch(IMyFunctionalBlock block)
        {
            if (!_watched.ContainsKey(block))
            {
                block.EnabledChanged += OnEnabledChanged;
                block.OnClose += OnBlockClosed;
            }
            _watched[block] = _pass;
        }

        /// <summary>Stops watching blocks the last check didn't find in any rule range.</summary>
        private void UnwatchMissed()
        {
            foreach (var pair in _watched)
            {
                if (pair.Value != _pass)
                    _unwatch.Add(pair.Key);
            }
            foreach (var block in _unwatch)
            {
                Unsubscribe(block);
                _watched.Remove(block);
            }
            _unwatch.Clear();
        }

        private void Unsubscribe(IMyFunctionalBlock block)
        {
            block.EnabledChanged -= OnEnabledChanged;
            block.OnClose -= OnBlockClosed;
        }

        private void OnBlockClosed(IMyEntity entity)
        {
            if (entity is IMyFunctionalBlock block && _watched.Remove(block))
                Unsubscribe(block);
        }

        /// <summary>Switches a watched block off again as soon as it is switched on inside a rule range.</summary>
        private void OnEnabledChanged(IMyTerminalBlock terminal)
        {
            try
            {
                if (!(terminal is IMyFunctionalBlock block) || !block.Enabled || block.Closed || IsNpc(block.OwnerId))
                    return;
                if (!(block.CubeGrid is MyCubeGrid grid) || IsNpcGrid(grid))
                    return;

                bool large = grid.GridSizeEnum == MyCubeSize.Large;
                foreach (var zone in _enforcing)
                {
                    string rule = RuleFor(zone.Profile, block, large, zone.Center, out double range);
                    if (rule == null)
                        continue;

                    block.Enabled = false;
                    Notify(block.OwnerId, zone.Name, rule, range, DateTime.UtcNow);
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "GVK KOTH: rule check on a switched-on block failed.");
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
