using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;

/*
namespace GameMod {
    [HarmonyPatch(typeof(Projectile), "Fire")]
    class CrusherTrails_Projectile_Fire {

        private static void ModifyCrusherTrail(Projectile proj) {
            if (!GameplayManager.IsMultiplayer) {
                proj.m_trail_renderer = FXTrailRenderer.trail_renderer_crusher + Projectile.CrusherNextTrailIndex;
                Projectile.CrusherNextTrailIndex = (Projectile.CrusherNextTrailIndex + 1) % 3;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldc_I4_6)
                    state++;

                if (state == 1) {
                    if (code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(Projectile), "CrusherNextTrailIndex")) {
                        state++;
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CrusherTrails_Projectile_Fire), "ModifyCrusherTrail"));
                        continue;
                    } else {
                        continue;
                    }
                }
                yield return code;
            }
        }
    }
}
*/