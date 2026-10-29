using System.Collections.Generic;

namespace GVK.Koth.Core.Config
{
    public enum ZoneMode
    {
        /// <summary>Shared capture: every faction in the zone fills one bar and scores when it fills.</summary>
        Capture,

        /// <summary>Factions with a powered station inside fill the bar at a flat speed.</summary>
        Encampment,

        /// <summary>No capture; only the rules below apply.</summary>
        RulesOnly
    }

    public enum ZoneAttach
    {
        /// <summary>Fixed coordinates (X, Y, Z).</summary>
        Coordinates,

        /// <summary>An NPC grid whose remote control CustomData has "GVK-KOTH=&lt;Name&gt;;" (from its MES behavior profile).</summary>
        NpcTag
    }

    public enum ZoneActivation
    {
        /// <summary>Runs all the time.</summary>
        AlwaysOn,

        /// <summary>Runs only while a slot has picked it.</summary>
        Slot,

        /// <summary>NPC zones: runs from the grid's spawn for LifetimeMinutes (0 = while the grid exists).</summary>
        Lifetime
    }

    /// <summary>
    /// One KOTH zone. Settings are copied from the King of the Hill mod where they existed.
    /// </summary>
    public class ZoneProfile
    {
        public string Name { get; set; } = "";
        public bool Enabled { get; set; } = true;
        public ZoneMode Mode { get; set; } = ZoneMode.Capture;
        public ZoneAttach Attach { get; set; } = ZoneAttach.Coordinates;
        public ZoneActivation Activation { get; set; } = ZoneActivation.Slot;

        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double Radius { get; set; } = 1500;

        /// <summary>NPC zones: minutes the zone runs after the grid spawns (0 = while the grid exists).</summary>
        public int LifetimeMinutes { get; set; }

        /// <summary>Optional MES spawn group spawned when a slot picks this zone (Flying KOTH).</summary>
        public string SpawnGroup { get; set; } = "";

        // --- Who counts ---
        public bool OnFoot { get; set; }
        public bool SmallGrids { get; set; } = true;
        public bool LargeGrids { get; set; } = true;
        public int MinSmallGridBlocks { get; set; } = 50;
        public int MinLargeGridBlocks { get; set; } = 100;

        /// <summary>Eligible players needed before the bar moves.</summary>
        public int MinPlayers { get; set; } = 1;

        // --- Capture ---
        /// <summary>Seconds to fill the bar with one player (faster with more, see CaptureCurve).</summary>
        public double CaptureSeconds { get; set; } = 300;

        /// <summary>The empty zone drains at this many players' speed (old IdleDrainRate). 0 = no drain.</summary>
        public int IdleDrainPlayers { get; set; } = 3;

        /// <summary>Share of the bar (0-1) a faction must have been present for to score when it fills. 0 = off.</summary>
        public double MinPresenceShare { get; set; }

        // --- Rewards ---
        public int PointsPerCompletion { get; set; } = 5;
        public long CreditsPerPoint { get; set; } = 200000;

        /// <summary>Zone points (all factions together) per loot drop. 0 = no loot.</summary>
        public int PointsPerLoot { get; set; } = 10;
        public List<LootItem> Loot { get; set; } = new List<LootItem>();

        /// <summary>Name of the public container inside the zone that receives the loot.</summary>
        public string LootContainerName { get; set; } = "KOTH Loot";

        // --- Rules (0 = off), enforced around the zone all the time ---
        public double NoThrusterRange { get; set; }
        public double NoLargeGridPowerRange { get; set; }
        public double NoSafeZoneRange { get; set; }

        /// <summary>NPC blocks tagged [GVK-KOTH-Link] within this range switch on while the zone runs. 0 = off.</summary>
        public double LinkRange { get; set; }
    }

    public class LootItem
    {
        /// <summary>Object builder type without the prefix, e.g. Component, Ingot, Ore.</summary>
        public string TypeId { get; set; } = "Component";
        public string SubtypeId { get; set; } = "";
        public int Amount { get; set; } = 1;
    }
}
