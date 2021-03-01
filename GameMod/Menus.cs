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
        public static bool mms_ctf_boost { get; set; }

        public static string GetMMSCtfCarrierBoost()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_ctf_boost));
        }

        private static bool _mms_scale_respawn_time;
        public static bool mms_scale_respawn_time
        {
            get { return _mms_scale_respawn_time && (MenuManager.mms_mode == ExtMatchMode.TEAM_ANARCHY || MenuManager.mms_mode == ExtMatchMode.CTF || MenuManager.mms_mode == ExtMatchMode.MONSTERBALL); }
            set { _mms_scale_respawn_time = value; }
        }
        public static bool mms_classic_spawns { get; set; }

        public static string GetMMSRearViewPIP()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(RearView.MPMenuManagerEnabled));
        }

        public static string GetMMSScaleRespawnTime()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_scale_respawn_time));
        }

        public static string GetMMSClassicSpawns()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_classic_spawns));
        }

        public static int mms_weapon_lag_compensation_max = 100;
        public static int mms_ship_lag_compensation_max = 100;
        public static int mms_weapon_lag_compensation_scale = 75;
        public static int mms_ship_lag_compensation_scale = 75;
        public static int mms_ship_max_interpolate_frames = 0;
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
            uie.SelectAndDrawStringOptionItem(Loc.LS("SCALE RESPAWN TO TEAM SIZE"), position, 12, Menus.GetMMSScaleRespawnTime(), Loc.LS("AUTOMATICALLY SCALE RESPAWN TIME TO TEAM SIZE (e.g. 4 = 4 seconds)"), 1f, !(MenuManager.mms_mode == ExtMatchMode.TEAM_ANARCHY || MenuManager.mms_mode == ExtMatchMode.CTF || MenuManager.mms_mode == ExtMatchMode.MONSTERBALL));
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("CLASSIC SPAWNS"), position, 13, Menus.GetMMSClassicSpawns(), Loc.LS("SPAWN WITH IMPULSE+ DUALS AND FALCONS"), 1f, false);
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("CTF CARRIER BOOSTING"), position, 14, Menus.GetMMSCtfCarrierBoost(), Loc.LS("FLAG CARRIER CAN USE BOOST IN CTF"), 1f, false);
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

    // Process Scale Respawn option
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class Menus_MenuManager_MpMatchSetup
    {
        static void Postfix()
        {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) &&
                MenuManager.m_menu_micro_state == 3 &&
                UIManager.m_menu_selection == 12)
            {
                Menus.mms_scale_respawn_time = !Menus.mms_scale_respawn_time;
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }

            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) &&
                MenuManager.m_menu_micro_state == 3 &&
                UIManager.m_menu_selection == 13)
            {
                Menus.mms_classic_spawns = !Menus.mms_classic_spawns;
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }

            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) &&
                MenuManager.m_menu_micro_state == 3 &&
                UIManager.m_menu_selection == 14)
            {
                Menus.mms_ctf_boost = !Menus.mms_ctf_boost;
                MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            }
        }       
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class Menus_UIElement_DrawMpMatchSetup_ScaleRespawnTime
    {
        // Change fade parameter on respawn slider from MenuManager.mms_mode == MatchMode.MONSTERBALL to Menus.mms_scale_respawn
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldstr && (string)code.operand == "RESPAWN TIME")
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(MenuManager), "mms_mode"))
                {
                    state = 2;
                    code.opcode = OpCodes.Call;
                    code.operand = AccessTools.Property(typeof(Menus), "mms_scale_respawn_time").GetGetMethod();
                }

                if (state == 2 && code.opcode == OpCodes.Ldc_I4_2)
                {
                    state = 3;
                    code.opcode = OpCodes.Ldc_I4_1;
                }

                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMpOptions")]
    class Menus_UIElement_DrawMpOptions
    {

        // Ugly hack, rewrite
        private static void SelectAndDrawSliderItem(UIElement uie, string s, Vector2 pos, int selection, float amt, float max)
        {
            float num = 750f;
            uie.TestMouseInRect(pos, num * 0.5f + 22f, 24f, selection, true);
            float x = pos.x;
            pos.x += num * 0.5f - 123f;
            uie.TestMouseInRectSlider(pos, 132f, 22f, selection, false);
            pos.x = x;
            bool flag = UIManager.m_menu_selection == selection;
            if (flag)
            {
                MenuManager.option_dir = true;
            }
            float num2 = 17f;
            Color c;
            Color color;
            Color color2;
            if (flag)
            {
                c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.2f * UIElement.FLICKER) + UIManager.m_select_flash * 0.05f);
                float a = 1f - Mathf.Pow(1f - uie.m_alpha, 8f);
                c.a = a;
                UIManager.DrawQuadBarHorizontal(pos, 22f, 22f, num, c, 12);
                color = UIManager.m_col_ub3;
                color.a = a;
                color2 = color;
                c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui7, UnityEngine.Random.Range(0f, 0.1f) + UIManager.m_select_flash * 0.5f);
                c.a = a;
            }
            else
            {
                c = Color.Lerp(UIManager.m_col_ui5, UIManager.m_col_ui6, UnityEngine.Random.Range(0f, 0.5f * UIElement.FLICKER));
                float num3 = 1f - Mathf.Pow(1f - uie.m_alpha, 6f);
                c.a = num3;
                UIManager.DrawQuadBarHorizontal(pos, 22f, 22f, num, c, 7);
                color2 = UIManager.m_col_ub0;
                color2.a = num3 * 0.1f;
                UIManager.DrawQuadBarHorizontal(pos, num2, num2, num, color2, 12);
                color = UIManager.m_col_ui2;
                color.a = uie.m_alpha;
                color2 = UIManager.m_col_ui0;
                color2.a = uie.m_alpha;
            }
            uie.DrawStringSmallOverrideAlpha(s, pos - Vector2.right * (num * 0.5f + 15f), 0.75f, StringOffset.LEFT, color2, 450f);
            UIManager.DrawQuadUI(pos + Vector2.right * 90f - Vector2.up * 15f, 20f, 1.25f, color2, uie.m_alpha, 12);
            uie.DrawDigitsThree(pos + Vector2.right * 90f, (int)(amt), 0.6f, StringOffset.CENTER, color, uie.m_alpha);
            UIManager.DrawQuadUI(pos + Vector2.right * 90f + Vector2.up * 15f, 20f, 1.25f, color2, uie.m_alpha, 12);
            pos.x += num * 0.5f - 123f;
            if (flag)
            {
                UIManager.DrawQuadBarHorizontal(pos, 17f, 17f, 246f, color, 12);
            }
            else
            {
                uie.DrawOutlineBackdrop(pos, 17f, 246f, color, 2);
            }
            UIManager.DrawQuadUIInner(pos - Vector2.right * (132f - (132f * (amt / max))), 132f * (amt / max), 10f, c, uie.m_alpha, 11, 0.75f);
        }

        static void DrawLagSliders(UIElement uie, ref Vector2 position)
        {
            SelectAndDrawSliderItem(uie, Loc.LS("WEAPON LAG COMPENSATION MAX"), position, 6, Menus.mms_weapon_lag_compensation_max, 250);
            position.y += 62f;
            SelectAndDrawSliderItem(uie, Loc.LS("SHIP LAG COMPENSATION MAX"), position, 7, Menus.mms_ship_lag_compensation_max, 250);
            position.y += 62f;
            SelectAndDrawSliderItem(uie, Loc.LS("WEAPON LAG COMPENSATION SCALE"), position, 8, Menus.mms_weapon_lag_compensation_scale, 100);
            position.y += 62f;
            SelectAndDrawSliderItem(uie, Loc.LS("SHIP LAG COMPENSATION SCALE"), position, 9, Menus.mms_ship_lag_compensation_scale, 100);
            position.y += 62f;
            SelectAndDrawSliderItem(uie, Loc.LS("SHIP LAG MAX INTERPOLATE FRAMES"), position, 10, Menus.mms_ship_max_interpolate_frames, 3);
            position.y += 62f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 155f) {
                    code.operand = 279f;
                }
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "QUICK CHAT")
                {
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawMpOptions), "DrawLagSliders"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpOptionsUpdate")]
    class Menus_MenuManager_MpOptionsUpdate
    {
        static void Postfix()
        {
            if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()) || UIManager.SliderMouseDown())
            {
                switch (UIManager.m_menu_selection)
                {
                    case 6:
                        Menus.mms_weapon_lag_compensation_max = (int)(UIElement.SliderPos * 250f);
                        break;
                    case 7:
                        Menus.mms_ship_lag_compensation_max = (int)(UIElement.SliderPos * 250f);
                        break;
                    case 8:
                        Menus.mms_weapon_lag_compensation_scale = (int)(UIElement.SliderPos * 100f);
                        break;
                    case 9:
                        Menus.mms_ship_lag_compensation_scale = (int)(UIElement.SliderPos * 100f);
                        break;
                    case 10:
                        Menus.mms_ship_max_interpolate_frames = (int)(UIElement.SliderPos * 3f + 0.5f);
                        break;
                    default:
                        break;
                }
            }                
        }
    }
}
