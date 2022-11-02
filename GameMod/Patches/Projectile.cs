using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches {
    /// <summary>
    /// Allows Thunderbolt projectiles to penetrate through ships.
    /// </summary>
    [Mod(Mods.ThunderboltPassthrough)]
    [HarmonyPatch(typeof(Projectile), "OnTriggerEnter")]
    public static class Projectile_OnTriggerEnter {
        public static void MaybeExplode(bool damaged_something, Projectile proj) {
            bool enablePassthrough = proj.m_type == ProjPrefab.proj_thunderbolt && GameplayManager.IsMultiplayer && ThunderboltPassthrough.Enabled;

            if (!enablePassthrough)
                proj.Explode(damaged_something);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Projectile), "Explode")) {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Projectile_OnTriggerEnter), "MaybeExplode"));
                    continue;
                }

                yield return code;
            }
        }
    }
}
