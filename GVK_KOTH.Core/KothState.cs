using System;
using System.Collections.Generic;
using System.Linq;

namespace GVK.Koth.Core
{
    /// <summary>
    /// Saved runtime state, so a restart keeps capture progress, loot owed and slot picks.
    /// </summary>
    public sealed class KothState
    {
        public List<ZoneSave> Zones { get; set; } = new List<ZoneSave>();
        public List<SlotPick> SlotPicks { get; set; } = new List<SlotPick>();

        public ZoneSave GetZone(string name)
        {
            var zone = Zones.FirstOrDefault(z => string.Equals(z.Name, name, StringComparison.OrdinalIgnoreCase));
            if (zone == null)
            {
                zone = new ZoneSave { Name = name };
                Zones.Add(zone);
            }
            return zone;
        }
    }

    public sealed class ZoneSave
    {
        public string Name { get; set; } = "";
        public double Progress { get; set; }
        public int LootCounter { get; set; }

        /// <summary>Loot drops earned but not yet delivered (container missing, concealed or full).</summary>
        public int PendingLootDrops { get; set; }
    }

    /// <summary>The zone a slot picked for one occurrence, so a restart mid-slot keeps the same zone.</summary>
    public sealed class SlotPick
    {
        public string Slot { get; set; } = "";
        public DateTime StartUtc { get; set; }
        public string Zone { get; set; } = "";
    }
}
