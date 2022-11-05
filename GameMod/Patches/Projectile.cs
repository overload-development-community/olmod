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
        public static void MaybeExplode(Projectile proj, bool damaged_something) {
            bool enablePassthrough = proj.m_type == ProjPrefab.proj_thunderbolt && GameplayManager.IsMultiplayer && ThunderboltPassthrough.Enabled;

            if (!enablePassthrough)
                proj.Explode(damaged_something);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Projectile), "Explode")) {
                    // Original call was this.Explode(bool).
                    // This replaces it with Projectile_OnTrigger.MaybeExplode(proj, damaged_something).
                    //
                    // But where does proj come from, you ask?
                    //
                    // Well, because this.Explode is a member function of class Projectile, it was already passing proj (aka this) in the original code.
                    // Therefore, we don't need to pass a second proj to the static function Projectile.OnTrigger.
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Projectile_OnTriggerEnter), "MaybeExplode"));
                    continue;
                }

                yield return code;
            }
        }
    }
}
