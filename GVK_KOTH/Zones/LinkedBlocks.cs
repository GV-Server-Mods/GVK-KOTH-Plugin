using System;
using System.Collections.Generic;
using System.Linq;
using GVK.Koth.Core;
using Sandbox.Game.Entities;
using Sandbox.Game.World;
using Sandbox.ModAPI;
using VRageMath;

namespace GVK.Koth.Zones
{
    /// <summary>
    /// NPC blocks tagged "[GVK-KOTH-Link]" or "[GVK-KOTH-Link:Zone]" in CustomData (e.g. KOTH Camp stores and
    /// medical rooms) switch on while their KOTH is open and off otherwise. Game thread only.
    /// </summary>
    public sealed class LinkedBlocks
    {
        private sealed class Link
        {
            public IMyFunctionalBlock Block;
            public string Zone; // null = any zone within that zone's LinkRange
        }

        private readonly Dictionary<long, List<Link>> _byGrid = new Dictionary<long, List<Link>>();

        public bool IsKnown(long gridId) => _byGrid.ContainsKey(gridId);

        /// <summary>Records a grid's tagged NPC blocks. Called once when the grid is scanned.</summary>
        public void Scan(MyCubeGrid grid)
        {
            if (_byGrid.ContainsKey(grid.EntityId))
                return;

            List<Link> links = null;
            foreach (var block in grid.GetFatBlocks())
            {
                if (!(block is IMyFunctionalBlock functional) || !IsNpc(functional.OwnerId))
                    continue;
                if (!NpcTag.ReadLink(functional.CustomData, out string zone))
                    continue;

                (links ??= new List<Link>()).Add(new Link { Block = functional, Zone = zone });
            }

            if (links != null)
                _byGrid[grid.EntityId] = links;
        }

        public void Clear() => _byGrid.Clear();

        /// <summary>Switches each linked block to match whether its KOTH is open.</summary>
        public void Apply(IReadOnlyList<ZoneRuntime> openZones)
        {
            foreach (var gridId in _byGrid.Keys.ToList())
            {
                var links = _byGrid[gridId];
                links.RemoveAll(l => l.Block == null || l.Block.Closed);
                if (links.Count == 0 || !MyEntities.TryGetEntityById(gridId, out var entity) || entity.Closed)
                {
                    _byGrid.Remove(gridId);
                    continue;
                }

                foreach (var link in links)
                {
                    bool on = ShouldBeOn(link, openZones);
                    if (link.Block.Enabled != on)
                        link.Block.Enabled = on;
                }
            }
        }

        private static bool ShouldBeOn(Link link, IReadOnlyList<ZoneRuntime> openZones)
        {
            var position = link.Block.GetPosition();
            for (int i = 0; i < openZones.Count; i++)
            {
                var zone = openZones[i];
                if (link.Zone != null)
                {
                    if (string.Equals(zone.Name, link.Zone, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
                else if (zone.Profile.LinkRange > 0
                    && Vector3D.DistanceSquared(position, zone.Center) <= zone.Profile.LinkRange * zone.Profile.LinkRange)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsNpc(long identityId)
        {
            return identityId != 0 && MySession.Static?.Players?.IdentityIsNpc(identityId) == true;
        }
    }
}
