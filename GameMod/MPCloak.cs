﻿using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace GameMod
{
    /// <summary>
    /// Issue #23 - Reduced Shader cloaked enemies are far too easy to see
    /// https://github.com/overload-development-community/olmod/issues/23
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "DrawEffectMesh")]
    class MPCloak_PlayerShip_DrawEffectMesh
    {
        public static float adjustedReducedShaderCloakOpacity
        {
            get
            {
                return GameplayManager.IsMultiplayerActive ? 0.3f : 1f;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(Robot), "matid_opacity"))
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 1f)
                {
                    state = 2;
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Property(typeof(MPCloak_PlayerShip_DrawEffectMesh), "adjustedReducedShaderCloakOpacity").GetGetMethod();
                }

                yield return code;
            }
        }
    }


    /// <summary>
    /// Old: UIElement.HUD_ALPHA = Mathf.Max(0.2f, UIElement.HUD_ALPHA - RUtility.FRAMETIME_GAME * 3f);
    /// New: UIElement.HUD_ALPHA = Mathf.Max(0.6f, UIElement.HUD_ALPHA - RUtility.FRAMETIME_GAME * 3f);
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    class MPCloak_PlayerShip_Update
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldfld && code.operand == AccessTools.Field(typeof(Player), "m_cloaked"))
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 0.2f)
                {
                    state++;
                    yield return new CodeInstruction(OpCodes.Ldc_R4, 0.6f);
                    continue;
                }

                yield return code;
            }
        }
    }
}
