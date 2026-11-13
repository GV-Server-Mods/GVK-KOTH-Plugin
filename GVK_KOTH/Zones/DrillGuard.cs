using System;
using System.Reflection;
using NLog;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities.Character;
using Sandbox.Game.Weapons;
using Sandbox.Game.World;
using Torch.Managers.PatchManager;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace GVK.Koth.Zones
{
    /// <summary>A zone's NoDrillRange sphere, as the drill guard sees it.</summary>
    public struct NoDrillZone
    {
        public BoundingSphereD Sphere;
        public string Name;
    }

    /// <summary>
    /// Server-side parts of NoDrillRange, as two game patches:
    /// - A player can't take out a hand drill inside a zone's drill range. The server refuses the equip request in
    ///   MyCharacter.CanSwitchToWeapon, the check Keen uses to stop the block placer in safe zones, so the client
    ///   ends up holding nothing (it puts its tool away before it asks).
    /// - A player hand drill or vanilla ship drill inside the range digs and damages nothing (MyDrillBase.Drill).
    ///   Needed because a ship drill fired from a cockpit drills even when switched off (MyShipDrill.CanShoot doesn't
    ///   check Enabled), and a hand drill carried in is only put away at the next rule check. Voxels are cut only on
    ///   the server (MyDrillBase.DrillVoxel), so this covers every client.
    /// ToolCore drills don't use MyDrillBase; switching them off is enough (ToolComp needs Enabled to hit).
    /// Torch registers [PatchShim] classes from plugin assemblies when plugins load and applies them at game start.
    /// </summary>
    [PatchShim]
    public static class DrillGuard
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        // Checked before patching: Torch throws while patching if a __field_ parameter names a missing field.
        private static readonly FieldInfo DrillEntityField =
            typeof(MyDrillBase).GetField("m_drillEntity", BindingFlags.NonPublic | BindingFlags.Instance);

        // Replaced whole by the rule enforcer on the game thread; read by the prefixes on the game thread.
        private static NoDrillZone[] _zones = new NoDrillZone[0];
        private static bool _drillErrorLogged;
        private static bool _equipErrorLogged;

        /// <summary>Called with the player's identity and the zone when a hand drill equip is refused.</summary>
        public static Action<long, NoDrillZone> EquipRefused;

        /// <summary>The no-drill zones enforcing rules now. Null or empty switches the guard off.</summary>
        public static void SetZones(NoDrillZone[] zones)
        {
            _zones = zones ?? new NoDrillZone[0];
        }

        public static void Patch(PatchContext ctx)
        {
            var drill = typeof(MyDrillBase).GetMethod("Drill", BindingFlags.Public | BindingFlags.Instance);
            var drillPrefix = typeof(DrillGuard).GetMethod(nameof(DrillPrefix), BindingFlags.NonPublic | BindingFlags.Static);
            if (drill == null || drillPrefix == null || DrillEntityField == null)
                Log.Error("GVK KOTH: MyDrillBase.Drill or m_drillEntity not found; hand drills and cockpit-fired ship drills can dig inside NoDrillRange.");
            else
                ctx.GetPattern(drill).Prefixes.Add(drillPrefix);

            var equip = typeof(MyCharacter).GetMethod("CanSwitchToWeapon", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(MyDefinitionId?) }, null);
            var equipPrefix = typeof(DrillGuard).GetMethod(nameof(EquipPrefix), BindingFlags.NonPublic | BindingFlags.Static);
            if (equip == null || equipPrefix == null)
                Log.Error("GVK KOTH: MyCharacter.CanSwitchToWeapon not found; hand drills can be taken out inside NoDrillRange.");
            else
                ctx.GetPattern(equip).Prefixes.Add(equipPrefix);
        }

        // __field_m_drillEntity: Torch passes the private field MyDrillBase.m_drillEntity (the hand drill or ship drill).
        private static bool DrillPrefix(MyEntity __field_m_drillEntity, ref bool __result)
        {
            var zones = _zones;
            if (zones.Length == 0)
                return true;

            try
            {
                var entity = __field_m_drillEntity;
                long owner;
                if (entity is MyHandDrill hand)
                    owner = hand.OwnerIdentityId;
                else if (entity is MyShipDrill ship)
                    owner = ship.OwnerId;
                else
                    return true;

                if (IsNpc(owner) || Find(zones, entity.PositionComp.GetPosition()) < 0)
                    return true;

                __result = false;
                return false;
            }
            catch (Exception e)
            {
                if (!_drillErrorLogged)
                {
                    _drillErrorLogged = true;
                    Log.Error(e, "GVK KOTH: drill guard failed; drilling is allowed where the check fails.");
                }
            }

            return true;
        }

        // The parameter name must match the game method's (weaponDefinition) for Torch to pass it.
        private static bool EquipPrefix(MyCharacter __instance, MyDefinitionId? weaponDefinition, ref bool __result)
        {
            var zones = _zones;
            if (zones.Length == 0 || !weaponDefinition.HasValue || weaponDefinition.Value.TypeId != typeof(MyObjectBuilder_HandDrill))
                return true;

            try
            {
                long identity = __instance.GetPlayerIdentityId();
                if (IsNpc(identity))
                    return true;

                int zone = Find(zones, __instance.PositionComp.GetPosition());
                if (zone < 0)
                    return true;

                EquipRefused?.Invoke(identity, zones[zone]);
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                if (!_equipErrorLogged)
                {
                    _equipErrorLogged = true;
                    Log.Error(e, "GVK KOTH: hand drill equip check failed; the equip is allowed where the check fails.");
                }
            }

            return true;
        }

        private static int Find(NoDrillZone[] zones, Vector3D position)
        {
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i].Sphere.Contains(position) != ContainmentType.Disjoint)
                    return i;
            }
            return -1;
        }

        private static bool IsNpc(long identityId)
        {
            return identityId != 0 && MySession.Static?.Players?.IdentityIsNpc(identityId) == true;
        }
    }
}
