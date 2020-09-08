using Harmony;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    public static class Menus
    {
        public static string GetMMSRearViewPIP()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(RearView.MPMenuManagerEnabled));
        }
    }


    /// <summary>
    /// Restructure Create Match -> Advanced to two columns
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class Menus_UIElement_DrawMpMatchSetup
    {

        private static float col_top;
        private static float col_bot;

        private static void AdjustAdvancedPositionLeftColumn(ref Vector2 position)
        {
            position.x -= 300f;
            col_top = position.y;
        }

        private static void DrawRightColumn(UIElement uie, ref Vector2 position)
        {
            col_bot = position.y;
            position.x += 600f;
            position.y = col_top - 250f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("ALLOW REAR VIEW CAMERA"), position, 11, Menus.GetMMSRearViewPIP(), Loc.LS("CLIENTS CAN CHOOSE TO HAVE REAR VIEW"), 1f, false);
            position.y += 62f;
        }

        private static void AdjustAdvancedPositionCenterColumn(ref Vector2 position)
        {
            position.x -= 300f;
            position.y = col_bot;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;

            foreach (var code in codes)
            {
                // Adjust x position
                if (state == 0 && code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(UIElement), "ToolTipActive"))
                {
                    state = 1;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawMpMatchSetup), "AdjustAdvancedPositionLeftColumn"));
                    continue;
                }

                // Readjust select width from 1.5 to 1.0
                if (state == 1 && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 1.5f)
                    code.operand = 1f;

                // Stop processing changes
                if (state == 1 && code.opcode == OpCodes.Ldstr && (string)code.operand == "POWERUP SETTINGS")
                {
                    state = 2;
                    // Draw things for right column
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawMpMatchSetup), "DrawRightColumn"));
                    // Revert to center column
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawMpMatchSetup), "AdjustAdvancedPositionCenterColumn"));
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Add Rear View option to Options -> Cockpit & HUD Options
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawHUDMenu")]
    class RearView_UIElement_DrawHUDMenu
    {
        static void DrawRearViewOption(UIElement uie, ref Vector2 pos)
        {
            pos.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("SHOW REAR VIEW CAMERA"), pos, 11, RearView.MenuManagerEnabled ? "ON" : "OFF", "SHOW REAR VIEW CAMERA ON HUD", 1.5f, false);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                // Bump up options towards header
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 248f)
                    code.operand = 310f;

                // Adjust top line slightly to compensate for squishing extra option in
                if (state == 0 && code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 40f)
                    code.operand = 30f;

                if (state == 0 && code.opcode == OpCodes.Ldstr && (string)code.operand == "SHOW CURRENT LEVEL AND MISSION TIME ON HUD")
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldarg_0)
                {
                    state = 2;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(RearView_UIElement_DrawHUDMenu), "DrawRearViewOption"));
                }

                yield return code;
            }
        }
    }
}
