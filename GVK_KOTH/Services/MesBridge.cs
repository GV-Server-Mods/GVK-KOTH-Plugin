using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sandbox.Game.GameSystems;
using VRageMath;

namespace GVK.Koth.Services
{
    /// <summary>
    /// Spawns MES spawn groups (Flying KOTH). MES sends its API dictionary to mods once at world load, before a
    /// Torch plugin's session hook runs, so the plugin asks MES's LocalApi.GetApiDictionary() for it directly.
    /// </summary>
    public sealed class MesBridge
    {
        private Func<List<string>, MatrixD, Vector3, bool, string, string, bool> _customSpawnRequest;
        private bool _searched;

        public bool Ready => _customSpawnRequest != null;

        private void Find()
        {
            if (_searched)
                return;
            _searched = true;

            try
            {
                var type = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("ModularEncountersSystems.API.LocalApi", false))
                    .FirstOrDefault(t => t != null);
                var method = type?.GetMethod("GetApiDictionary", BindingFlags.Public | BindingFlags.Static);
                if (!(method?.Invoke(null, null) is Dictionary<string, Delegate> api))
                {
                    Plugin.Log.Warn("GVK KOTH: MES API not found; Flying KOTH can't spawn.");
                    return;
                }

                if (api.TryGetValue("CustomSpawnRequest", out var spawn))
                    _customSpawnRequest = spawn as Func<List<string>, MatrixD, Vector3, bool, string, string, bool>;
                if (_customSpawnRequest == null)
                    Plugin.Log.Warn("GVK KOTH: MES API has no CustomSpawnRequest of the expected type.");
            }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "GVK KOTH: failed to read the MES API.");
            }
        }

        /// <summary>
        /// Spawns the spawn group upright at the position (up = away from gravity). The spawn group needs
        /// [RivalAiSpawn:true] in its conditions. Returns false if MES isn't available or refused.
        /// </summary>
        public bool Spawn(string spawnGroup, Vector3D position)
        {
            Find();
            if (_customSpawnRequest == null)
                return false;

            var gravity = MyGravityProviderSystem.CalculateNaturalGravityInPoint(position);
            var up = gravity.LengthSquared() > 0.0001 ? -Vector3D.Normalize(gravity) : Vector3D.Up;
            var forward = Vector3D.CalculatePerpendicularVector(up);
            var matrix = MatrixD.CreateWorld(position, forward, up);

            return _customSpawnRequest(new List<string> { spawnGroup }, matrix, Vector3.Zero, true, null, null);
        }
    }
}
