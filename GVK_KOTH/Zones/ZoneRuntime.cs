using GVK.Koth.Core;
using GVK.Koth.Core.Config;
using VRageMath;

namespace GVK.Koth.Zones
{
    /// <summary>
    /// A configured zone while the session runs: its profile, capture bar and loot queue.
    /// </summary>
    public sealed class ZoneRuntime
    {
        public ZoneRuntime(ZoneProfile profile, ZoneSave save)
        {
            Profile = profile;
            Save = save;
            Capture = new ZoneCapture(new CaptureRules
            {
                CaptureSeconds = profile.CaptureSeconds,
                MinPlayers = profile.Mode == ZoneMode.Encampment ? 1 : profile.MinPlayers,
                IdleDrainPlayers = profile.IdleDrainPlayers,
                MinPresenceShare = profile.MinPresenceShare,
                PointsPerCompletion = profile.PointsPerCompletion,
                PointsPerLoot = profile.PointsPerLoot,
                FlatSpeed = profile.Mode == ZoneMode.Encampment
            })
            {
                Progress = save.Progress,
                LootCounter = save.LootCounter
            };
        }

        public ZoneProfile Profile { get; }
        public ZoneSave Save { get; }
        public ZoneCapture Capture { get; }

        public string Name => Profile.Name;
        public bool Active { get; set; }

        /// <summary>Where the zone is now. Fixed zones use their coordinates.</summary>
        public Vector3D Center => new Vector3D(Profile.X, Profile.Y, Profile.Z);

        /// <summary>Factions counted at the last check, for status and the HUD.</summary>
        public int LastParticipantCount { get; set; }

        public long LootContainerId;
        public double SecondsToNextLootAttempt;

        /// <summary>Copies the bar and loot counter into the saved state.</summary>
        public void StoreState()
        {
            Save.Progress = Capture.Progress;
            Save.LootCounter = Capture.LootCounter;
        }
    }
}
