using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod.CMTracker
{
    class CMTracker
    {
        public static bool mms_cm_runs_visible_in_tracker = false;
    }

    [HarmonyPatch(typeof(UIElement), "DrawChallengeLevelSelectMenu")]
    class CMTracker_Menu_UIElement_DrawChallengeLevelSelectMenu
    {

        private static void PatchMenu(UIElement uie, Vector2 position)
        {
            uie.SelectAndDrawCheckboxItem("SUBMIT RESULTS TO PUBLIC TRACKER", position, 7, CMTracker.mms_cm_runs_visible_in_tracker, false, 1f, -1);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            // Skip from float num = 137f; to end of method, not applicable to olmod
            bool skip = false;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 137f)
                    skip = true;

                if (skip)
                    continue;

                yield return code;
            }

            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Ldloc_0);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CMTracker_Menu_UIElement_DrawChallengeLevelSelectMenu), "PatchMenu"));
            yield return new CodeInstruction(OpCodes.Ret);
        }
    }

    [HarmonyPatch(typeof(MenuManager), "ChallengeLevelSelectUpdate")]
    internal class CMTracker_Menu_MenuManager_ChallengeLevelSelectUpdate
    {
        private static int PatchMenu()
        {
            if (UIManager.m_menu_selection == 7)
            {
                CMTracker.mms_cm_runs_visible_in_tracker = !CMTracker.mms_cm_runs_visible_in_tracker;
                MenuManager.PlaySelectSound();
                return UIManager.m_menu_selection;
            }

            // ilcode represented differently in Harmony than dnSpy, we need to return something other than UIManager.m_menu_selection to hit CreateNewGame branch
            return 99;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 97)
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(CMTracker_Menu_MenuManager_ChallengeLevelSelectUpdate), "PatchMenu"));
                    continue;
                }
                yield return code;
            }
        }
    }
}
