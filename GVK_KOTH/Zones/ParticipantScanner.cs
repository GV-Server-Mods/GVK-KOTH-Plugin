using System.Collections.Generic;
using GVK.Koth.Core.Config;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRageMath;

namespace GVK.Koth.Zones
{
    /// <summary>
    /// Finds who counts toward a zone's bar. Game thread only.
    /// Capture: one entry per eligible player (faction id). Encampment: one entry per eligible station.
    /// </summary>
    public static class ParticipantScanner
    {
        private static readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        public static List<long> Capture(ZoneProfile zone, Vector3D center)
        {
            var result = new List<long>();
            double radiusSq = zone.Radius * zone.Radius;

            Players.Clear();
            MyAPIGateway.Players.GetPlayers(Players);
            foreach (var player in Players)
            {
                if (player == null || player.IsBot)
                    continue;

                var character = player.Character;
                if (character == null || character.IsDead || character.Closed)
                    continue;
                if (Vector3D.DistanceSquared(character.GetPosition(), center) > radiusSq)
                    continue;
                if (!IsEligible(zone, player))
                    continue;

                var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                if (faction == null || faction.IsEveryoneNpc())
                    continue;

                result.Add(faction.FactionId);
            }

            Players.Clear();
            return result;
        }

        /// <summary>
        /// On foot counts if the zone allows it. A seated player counts if the grid they sit on (not remote control)
        /// matches the zone's grid sizes, has enough blocks, is powered and isn't static.
        /// </summary>
        private static bool IsEligible(ZoneProfile zone, IMyPlayer player)
        {
            var controlled = player.Controller?.ControlledEntity?.Entity;
            if (controlled is IMyCharacter)
                return zone.OnFoot;

            if (!(controlled is IMyCockpit cockpit))
                return false;

            if (!(cockpit.CubeGrid is MyCubeGrid grid) || grid.IsStatic)
                return false;

            bool small = grid.GridSizeEnum == VRage.Game.MyCubeSize.Small;
            if (small ? !zone.SmallGrids : !zone.LargeGrids)
                return false;
            if (grid.BlocksCount < (small ? zone.MinSmallGridBlocks : zone.MinLargeGridBlocks))
                return false;

            return IsPowered(grid);
        }

        public static List<long> Encampment(ZoneProfile zone, Vector3D center)
        {
            var result = new List<long>();
            var sphere = new BoundingSphereD(center, zone.Radius);
            foreach (var entity in MyEntities.GetTopMostEntitiesInSphere(ref sphere))
            {
                if (!(entity is MyCubeGrid grid) || !grid.IsStatic || grid.Closed)
                    continue;
                if (grid.BlocksCount < zone.MinLargeGridBlocks || grid.BigOwners.Count == 0)
                    continue;
                if (Vector3D.DistanceSquared(grid.PositionComp.WorldAABB.Center, center) > zone.Radius * zone.Radius)
                    continue;

                var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
                if (faction == null || faction.IsEveryoneNpc())
                    continue;

                if (IsPowered(grid))
                    result.Add(faction.FactionId);
            }
            return result;
        }

        public static bool IsPowered(MyCubeGrid grid)
        {
            var distributor = grid.GridSystems?.ResourceDistributor;
            return distributor != null
                && distributor.ResourceStateByType(MyResourceDistributorComponent.ElectricityId, withRecompute: false) != VRage.MyResourceStateEnum.NoPower;
        }
    }
}
