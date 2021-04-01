using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    // Original orange color overlay deemed too intense in most MP situations, allow for reducing the alpha
    [HarmonyPatch(typeof(UIManager), "DrawFullScreenEffects")]
    class MPDamageEffects_UIManager_DrawFullScreenEffects
    {
        static void AdjustAlpha(ref float a)
        {
            PlayerShip player_ship = GameManager.m_player_ship;

            // Original alpha
            a = UIManager.menu_flash_fade * Mathf.Min(0.1f, player_ship.m_damage_flash_slow * 0.1f) + Mathf.Min(0.2f, player_ship.m_damage_flash_fast * 0.2f) * (GameplayManager.IsMultiplayerActive ? ((float)Menus.mms_damageeffect_alpha_mult) / 100f : 1f);
        }

        // Change damage color
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Stloc_2)
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloca, 2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPDamageEffects_UIManager_DrawFullScreenEffects), "AdjustAlpha"));
                    state = 1;
                    continue;
                }

                yield return code;
            }
        }
    }

    // Original drunk blur effect on damage deemed too intense in many MP situations, allow adjustment
    [HarmonyPatch(typeof(Viewer), "UpdateCameraBlurs")]
    class MPDamageEffects_Viewer_UpdateCameraBlurs
    {
        static void Postfix(DrunkBlur ___m_drunk_blur)
        {
            if (___m_drunk_blur != null)
            {
                ___m_drunk_blur.strength *= GameplayManager.IsMultiplayerActive ? ((float)Menus.mms_damageeffect_drunk_blur_mult) / 100f : 1f;
            }
                
        }
    }
}
