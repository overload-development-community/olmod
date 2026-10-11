using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Replay
{
    public static class Shots
    {
        private struct InjectedShot
        {
            public Projectile projectile;
            public float bornAtClock;
        }

        private static readonly Dictionary<int, InjectedShot> liveShots = new Dictionary<int, InjectedShot>();
        private static readonly List<int> staleFireIds = new List<int>();
        private static readonly HashSet<Projectile> trackedProjectiles = new HashSet<Projectile>();

        private static int pendingFireId = -1;

        private static GameObject fireOwner;

        private static float nextUntrackedSweepTime;

        public static void Begin()
        {
            liveShots.Clear();
            pendingFireId = -1;
            fireOwner = new GameObject("ReplayFireOwner");
            var body = fireOwner.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        public static void End()
        {
            ClearAll();
            if (fireOwner != null)
            {
                UnityEngine.Object.Destroy(fireOwner);
                fireOwner = null;
            }
        }

        public static void Inject(MatchEvent fire)
        {
            try
            {
                if (fireOwner != null)
                    fireOwner.transform.position = fire.pos;
                pendingFireId = fire.iC;
                ProjectileManager.FireProjectile((ProjPrefab)fire.iB, fire.pos, fire.rot, fireOwner,
                    0f, ProjTeam.PLAYER, (WeaponUnlock)(int)fire.fA);
            }
            finally
            {
                pendingFireId = -1;
            }
        }

        public static void Terminate(int fireId, bool exploded, Vector3 pos)
        {
            InjectedShot shot;
            if (!liveShots.TryGetValue(fireId, out shot))
                return;
            liveShots.Remove(fireId);
            var projectile = shot.projectile;

            if (projectile == null || !projectile.m_alive || projectile.m_projectile_id != fireId)
                return;

            if (pos != Vector3.zero && projectile.c_transform != null)
                projectile.c_transform.position = pos;
            if (exploded)
                projectile.Explode();
            else
                projectile.KillInstant();
        }

        public static void ClearAll()
        {
            ProjectileManager.DestroyAll();
            liveShots.Clear();
            pendingFireId = -1;
        }

        public static void Drive()
        {
            if (Time.unscaledTime >= nextUntrackedSweepTime)
            {
                nextUntrackedSweepTime = Time.unscaledTime + 0.5f;
                KillUntracked();
            }
            if (liveShots.Count == 0)
                return;

            staleFireIds.Clear();
            float clock = Playback.Clock;
            foreach (var entry in liveShots)
            {
                var projectile = entry.Value.projectile;
                bool goneOrRecycled = projectile == null || !projectile.m_alive ||
                                      projectile.m_projectile_id != entry.Key;
                if (goneOrRecycled)
                {
                    staleFireIds.Add(entry.Key);
                    continue;
                }
                if ((clock - entry.Value.bornAtClock) > 10f)
                {
                    projectile.KillInstant();
                    staleFireIds.Add(entry.Key);
                }
            }
            for (int i = 0; i < staleFireIds.Count; i++)
                liveShots.Remove(staleFireIds[i]);
        }

        private static void KillUntracked()
        {
            var pools = ProjectileManager.proj_list;
            if (pools == null)
                return;
            trackedProjectiles.Clear();
            foreach (var entry in liveShots)
                if (entry.Value.projectile != null)
                    trackedProjectiles.Add(entry.Value.projectile);
            foreach (var pool in pools)
            {
                if (pool == null)
                    continue;
                for (int i = 0; i < pool.Count; i++)
                {
                    var projectile = pool[i] != null ? pool[i].c_proj : null;
                    if (projectile == null || !projectile.m_alive ||
                        trackedProjectiles.Contains(projectile) ||
                        Recorder.IsUnrecordedFireType(projectile.m_type))
                        continue;
                    projectile.KillInstant();
                }
            }
        }

        [HarmonyPatch(typeof(Projectile), "Fire")]
        class Shots_Projectile_Fire
        {
            static void Postfix(Projectile __instance)
            {
                if (!Playback.IsActive || pendingFireId < 0 || __instance == null)
                    return;
                liveShots[pendingFireId] = new InjectedShot
                {
                    projectile = __instance,
                    bornAtClock = Playback.Clock
                };

                __instance.m_projectile_id = pendingFireId;

                __instance.m_homing_strength = 0f;
            }
        }

        [HarmonyPatch(typeof(Projectile), "IsOwnedByPlayer")]
        class Shots_IsOwnedByPlayer
        {
            static bool Prefix(ref bool __result)
            {
                if (!Playback.IsActive || pendingFireId < 0)
                    return true;
                __result = true;
                return false;
            }
        }
    }

    public static class MultiplayerProjectiles
    {
        public static bool MultiplayerBehaviour()
        {
            return GameplayManager.IsMultiplayerActive || Playback.Ready;
        }

        private static readonly FieldInfo isMultiplayerActiveField =
            AccessTools.Field(typeof(GameplayManager), "IsMultiplayerActive");
        private static readonly MethodInfo isMultiplayerGetter =
            AccessTools.PropertyGetter(typeof(GameplayManager), "IsMultiplayer");
        private static readonly MethodInfo multiplayerBehaviourMethod =
            AccessTools.Method(typeof(MultiplayerProjectiles), "MultiplayerBehaviour");

        private static IEnumerable<CodeInstruction> RewriteMultiplayerTests(string methodName,
            IEnumerable<CodeInstruction> instructions)
        {
            int rewritten = 0;
            foreach (var instruction in instructions)
            {
                bool isFieldRead = instruction.opcode == OpCodes.Ldsfld &&
                                   isMultiplayerActiveField != null &&
                                   (instruction.operand as FieldInfo) == isMultiplayerActiveField;
                bool isPropertyRead = (instruction.opcode == OpCodes.Call ||
                                       instruction.opcode == OpCodes.Callvirt) &&
                                      isMultiplayerGetter != null &&
                                      (instruction.operand as MethodInfo) == isMultiplayerGetter;
                if ((isFieldRead || isPropertyRead) && multiplayerBehaviourMethod != null)
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = multiplayerBehaviourMethod;
                    rewritten++;
                }
                yield return instruction;
            }

            UnityEngine.Debug.Log(rewritten > 0
                ? "REPLAY: " + methodName + " -- " + rewritten + " multiplayer tests rewritten"
                : "REPLAY: " + methodName + " -- NO multiplayer test found, replayed shots will "
                  + "use singleplayer behaviour");
        }

        [HarmonyPatch(typeof(Projectile), "Fire")]
        class MultiplayerProjectiles_Projectile_Fire
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return RewriteMultiplayerTests("Projectile.Fire", instructions);
            }
        }

        [HarmonyPatch(typeof(Projectile), "FixedUpdateDynamic")]
        class MultiplayerProjectiles_Projectile_FixedUpdateDynamic
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                return RewriteMultiplayerTests("Projectile.FixedUpdateDynamic", instructions);
            }
        }
    }
}
