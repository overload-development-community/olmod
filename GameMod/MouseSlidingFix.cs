using System.Collections.Generic;
using Overload;
using UnityEngine;
using HarmonyLib;
using System.Reflection.Emit;


namespace GameMod
{
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdateProcessControlsInternal")]
    public static class MouseSlidingFix_PlayerShip_FixedUpdateProcessControlsInternal
    {
        /*
         * 
            zero.x *= (float)c_player.m_player_control_options.opt_mouse_sens_x * 0.01f;
			zero.y *= (float)c_player.m_player_control_options.opt_mouse_sens_y * 0.01f;
			zero.z *= (float)c_player.m_player_control_options.opt_mouse_sens_y * 0.01f;
            ...
			num4 += zero.x; -- becomes --> num4 = Mathf.Clamp(num4 + zero.x, -1f, 1f);
			num3 += zero.y; -- becomes --> num4 = Mathf.Clamp(num3 + zero.y, -1f, 1f);
			num5 += zero.z; -- becomes --> num4 = Mathf.Clamp(num5 + zero.z, -1f, 1f);
         *
         */

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int count = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Stloc_2 || code.opcode == OpCodes.Stloc_3 || (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalIndex == 4))
                {
                    count++;
                        
                    if (count > 3 && count < 7)
                    {
                        yield return new CodeInstruction(OpCodes.Ldc_R4, -1f);
                        yield return new CodeInstruction(OpCodes.Ldc_R4, 1f);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Mathf), "Clamp", new System.Type[] { typeof(float), typeof(float), typeof(float) }));
                    }                      
                }
                yield return code;
            }
        }
    }
}
