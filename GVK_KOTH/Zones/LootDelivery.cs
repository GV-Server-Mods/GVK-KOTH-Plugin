using System;
using System.Collections.Generic;
using System.Linq;
using GVK.Koth.Core.Config;
using Sandbox.Game.Entities;
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

        private static IMyCargoContainer FindContainer(ZoneProfile zone, Vector3D center, ref long cachedId)
        {
            double radiusSq = zone.Radius * zone.Radius;

            if (cachedId != 0 && MyEntities.TryGetEntityById(cachedId, out var cached)
                && cached is IMyCargoContainer known && !known.Closed && known.IsFunctional
                && Vector3D.DistanceSquared(known.GetPosition(), center) <= radiusSq
                && NameMatches(known, zone))
                return known;

            cachedId = 0;
            var sphere = new BoundingSphereD(center, zone.Radius);
            foreach (var entity in MyEntities.GetTopMostEntitiesInSphere(ref sphere))
            {
                if (!(entity is MyCubeGrid grid) || grid.Closed)
                    continue;

                foreach (var block in grid.GetFatBlocks())
                {
                    if (block is IMyCargoContainer cargo && cargo.IsFunctional && NameMatches(cargo, zone)
                        && Vector3D.DistanceSquared(cargo.GetPosition(), center) <= radiusSq)
                    {
                        cachedId = cargo.EntityId;
                        return cargo;
                    }
                }
            }
            return null;
        }

        private static bool NameMatches(IMyTerminalBlock block, ZoneProfile zone)
        {
            return string.Equals(block.CustomName?.Trim(), zone.LootContainerName?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public static string Gps(string name, Vector3D position)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return $"GPS:{name}:{position.X.ToString("0.##", c)}:{position.Y.ToString("0.##", c)}:{position.Z.ToString("0.##", c)}:";
        }
    }
}
