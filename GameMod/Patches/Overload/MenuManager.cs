using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Metadata;
using HarmonyLib;
using Overload;

namespace GameMod.Patches {
    /// <summary>
    /// Stock game shows 60/30hz for what is actually "full/half" sync rates in Unity, simply change labels
    /// </summary>
    [Mod(Mods.VSync)]
    [HarmonyPatch(typeof(MenuManager), "GetVSyncSetting")]
    public class MenuManager_GetVSyncSetting {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
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
    /// Stock game did not actually implement reverse arrow on vsync option.
    /// </summary>
    /// <remarks>
    /// Original: MenuManager.gfx_vsync = (MenuManager.gfx_vsync + 3 - 1) % 3
    /// New:      MenuManager.gfx_vsync = (MenuManager.gfx_vsync + 3 + 1 - 1 + UIManager.m_select_dir) % 3
    /// </remarks>
    [Mod(Mods.VSync)]
    [HarmonyPatch(typeof(MenuManager), "GraphicsOptionsUpdate")]
    public class MenuManager_GraphicsOptionsUpdate {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {

                if (state == 0 && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(MenuManager), "gfx_vsync"))
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldc_I4_3) {
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
