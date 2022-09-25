using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Overload;

namespace GameMod.Patches {
    /// <summary>
    /// Mod: VSync
    /// Author: Tobias
    /// 
    /// Stock game shows 60/30hz for what is actually "full/half" sync rates in Unity, simply change labels
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), "GetVSyncSetting")]
    class MenuManager_GetVSyncSetting {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "60 HZ")
                    code.operand = "100% MONITOR RATE";

                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "30 HZ")
                    code.operand = "50% MONITOR RATE";

                yield return code;
            }
        }
    }

    /// <summary>
    /// Mod: VSync
    /// Author: Tobias
    /// 
    /// Stock game did not actually implement reverse arrow on vsync option
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), "GraphicsOptionsUpdate")]
    class VSync_MenuManager_GraphicsOptionsUpdate {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {

                if (state == 0 && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(MenuManager), "gfx_vsync"))
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldc_I4_3) {
                    // Original: MenuManager.gfx_vsync = (MenuManager.gfx_vsync + 3 - 1) % 3
                    // New:      MenuManager.gfx_vsync = (MenuManager.gfx_vsync + 3 + 1 - 1 + UIManager.m_select_dir) % 3
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(UIManager), "m_select_dir"));
                    yield return new CodeInstruction(OpCodes.Add);
                    state = 2;
                }

                yield return code;
            }
        }
    }
}
