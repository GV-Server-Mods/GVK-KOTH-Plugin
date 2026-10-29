using System;
using System.Collections.Generic;
using System.Linq;
using GVK.Koth.Core.Config;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace GVK.Koth.Zones
{
    /// <summary>
    /// Puts loot into a zone's public container, found by name inside the zone. Game thread only.
    /// </summary>
    public static class LootDelivery
    {
        private static readonly List<VRage.Game.Entity.MyEntity> Entities = new List<VRage.Game.Entity.MyEntity>();

        /// <summary>
        /// Delivers one drop. Returns false (nothing added) if the container is missing, unloaded or too full,
        /// so the drop stays queued.
        /// </summary>
        public static bool TryDeliver(ZoneProfile zone, Vector3D center, ref long cachedContainerId, out IMyCargoContainer container)
        {
            container = FindContainer(zone, center, ref cachedContainerId);
            if (container == null)
                return false;

            var inventory = container.GetInventory();
            var items = new List<(MyDefinitionId Id, MyObjectBuilder_PhysicalObject Builder, int Amount)>();
            foreach (var loot in zone.Loot)
            {
                if (!MyObjectBuilderType.TryParse("MyObjectBuilder_" + loot.TypeId, out var type))
                {
                    Plugin.Instance?.Alerts.Send($"Zone '{zone.Name}': unknown loot type '{loot.TypeId}'.");
                    continue;
                }

                var id = new MyDefinitionId(type, loot.SubtypeId);
                if (!(MyObjectBuilderSerializer.CreateNewObject(id) is MyObjectBuilder_PhysicalObject builder))
                {
                    Plugin.Instance?.Alerts.Send($"Zone '{zone.Name}': '{loot.TypeId}/{loot.SubtypeId}' is not an item.");
                    continue;
                }
                items.Add((id, builder, loot.Amount));
            }

            // All or nothing: a half-delivered drop would be hard to account for.
            if (items.Count == 0 || items.Any(i => !inventory.CanItemsBeAdded(i.Amount, i.Id)))
                return false;

            foreach (var item in items)
                inventory.AddItems(item.Amount, item.Builder);
            return true;
        }

        /// <summary>
        /// The one loot container for this zone: named LootContainerName, inside the zone, on a grid an NPC owns
        /// (majority owner), nearest the zone centre. Player-built containers never qualify, even unowned ones.
        /// </summary>
        private static IMyCargoContainer FindContainer(ZoneProfile zone, Vector3D center, ref long cachedId)
        {
            double radiusSq = zone.Radius * zone.Radius;

            if (cachedId != 0 && MyEntities.TryGetEntityById(cachedId, out var cached)
                && cached is IMyCargoContainer known && IsValid(known, zone, center, radiusSq))
                return known;

            cachedId = 0;
            IMyCargoContainer best = null;
            double bestDistSq = double.MaxValue;

            Entities.Clear();
            var sphere = new BoundingSphereD(center, zone.Radius);
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, Entities);
            foreach (var entity in Entities)
            {
                if (!(entity is MyCubeGrid grid) || grid.Closed || !IsNpcGrid(grid))
                    continue;

                foreach (var block in grid.GetFatBlocks())
                {
                    if (!(block is IMyCargoContainer cargo) || !IsValid(cargo, zone, center, radiusSq))
                        continue;

                    double distSq = Vector3D.DistanceSquared(cargo.GetPosition(), center);
                    if (distSq < bestDistSq)
                    {
                        best = cargo;
                        bestDistSq = distSq;
                    }
                }
            }
            Entities.Clear();

            if (best != null)
                cachedId = best.EntityId;
            return best;
        }

        private static bool IsValid(IMyCargoContainer cargo, ZoneProfile zone, Vector3D center, double radiusSq)
        {
            return !cargo.Closed && cargo.IsFunctional && NameMatches(cargo, zone)
                && cargo.CubeGrid is MyCubeGrid grid && IsNpcGrid(grid)
                && Vector3D.DistanceSquared(cargo.GetPosition(), center) <= radiusSq;
        }

        private static bool IsNpcGrid(MyCubeGrid grid)
        {
            return grid.BigOwners.Count > 0 && MySession.Static?.Players?.IdentityIsNpc(grid.BigOwners[0]) == true;
        }

        private static bool NameMatches(IMyTerminalBlock block, ZoneProfile zone)
        {
            return string.Equals(block.CustomName?.Trim(), zone.LootContainerName?.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
