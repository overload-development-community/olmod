using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    /// <summary>
    ///  Goal:    providing the option to set a curve for each controller axis.
    ///           The curve gets created through 4 points. 2 of them are fixed along the x axis while the other two are 
    ///           freely moveable (within (0,0)->(1,1)) and act as "weights" to form a 4 point Bezier curve.      
    /// 
    ///  Author:  luponix 
    ///  Created: 2021-04-15
    /// </summary>
    class JoystickCurveEditor
    {
        /*
        internal class DebugOutput
        {
            public static InputAdjustment[] axes = new InputAdjustment[100];

            public class InputAdjustment
            {
                public int controller_num;
                public int control_num;
                public float last_original_input;
                public float last_adjusted_input;
            }

            [HarmonyPatch(typeof(UIElement), "DrawHUD")]
            class JoystickCurveEditor_DebugOutput_UIElement_DrawHUD
            {
                static void Postfix(UIElement __instance)
                {

                    Vector2 pos = new Vector2(-625f, -300f);
                    for (int i = 0; i < axes.Length; i++)
                    {
                        if (axes[i] != null)
                        {
                            __instance.DrawStringSmall(Controls.m_controllers[axes[i].controller_num].name + ":" + axes[i].control_num, pos, 0.32f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                            pos.x += 260f;
                            __instance.DrawStringSmall(axes[i].last_original_input.ToString("n3"), pos, 0.45f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                            pos.x += 65f;
                            __instance.DrawStringSmall(axes[i].last_adjusted_input.ToString("n3"), pos, 0.45f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                            pos.y += 18f;
                            pos.x -= 260f;
                            pos.x -= 65f;
                            axes[i].last_original_input = 0;
                            axes[i].last_adjusted_input = 0;
                        }
                    }
                    pos.y += 38f;
                    __instance.DrawStringSmall("Turnrate        :", pos, 0.45f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                    pos.x += 260f;
                    __instance.DrawStringSmall( GameManager.m_local_player.c_player_ship.c_rigidbody.angularVelocity.magnitude.ToString("n3"), pos, 0.45f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                }
            }
        }
        */

        

        // Adds an "EDIT CURVE" Button under "Options/Control Options/Joystick/Axis" 
        [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
        internal class JoystickCurveEditor_DrawControlsMenu
        {
            private static void DrawEditCurveOption(UIElement uie, ref Vector2 position)
            {
                position.y += 62f;
                uie.SelectAndDrawItem(Loc.LS("EDIT CURVE"), position, 12, false, 1f, 0.75f);
                position.y -= 20f;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                int state = 0;
                for (int i = 0; i < codes.Count; i++)
                {
                    // adjusts the spacing to make some room
                    if (state == 0 && codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "CONTROLLER DISCONNECTED" && codes[i + 11].opcode == OpCodes.Ldc_R4)
                    {
                        state++;
                        codes[i + 11].operand = -230f;
                    }
                    if (state == 1 && codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "CURRENT SETTINGS:  MIN {0}  MAX {1}  ZERO {2}")
                    {
                        state++;
                        if (codes[i - 13].opcode == OpCodes.Ldc_R4) codes[i - 13].operand = 71f;
                    }

                    // adds the Edit Curve Button
                    if (state == 2 && codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "CALIBRATE")
                    {
                        var newCodes = new[] {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldloca, 0),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JoystickCurveEditor_DrawControlsMenu), "DrawEditCurveOption"))
                        };
                        codes.InsertRange(i + 7, newCodes);
                        break;
                    }
                }
                return codes;
            }
        }

        // changes the menu state if the "EDIT CURVE" Button gets pressed
        [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
        class JoystickCurveEditor_MenuManager_ControlsOptionsUpdate
        {
            static void Postfix()
            {
                if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()) || UIManager.SliderMouseDown())
                {
                    switch (UIManager.m_menu_selection)
                    {
                        case 12:
                            MenuManager.ChangeMenuState(Menus.msAxisCurveEditor, false);
                            UIManager.DestroyAll(false);
                            MenuManager.PlaySelectSound(1f);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        // process the curve editor logic if we entered this menustate
        private static int move_point = -1;
        [HarmonyPatch(typeof(MenuManager), "Update")]
        class JoystickCurveEditor_MenuManager_Update
        {

            private static void Postfix(ref float ___m_menu_state_timer)
            {
                if (MenuManager.m_menu_state == Menus.msAxisCurveEditor)
                    JoystickCurveEditorUpdate(ref ___m_menu_state_timer);
            }


            private static void JoystickCurveEditorUpdate(ref float m_menu_state_timer)
            {
                UIManager.MouseSelectUpdate();
                switch (MenuManager.m_menu_sub_state)
                {
                    case MenuSubState.INIT:
                        if (m_menu_state_timer > 0.25f)
                        {
                            UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, Menus.uiAxisCurveEditor);
                            MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                            m_menu_state_timer = 0f;
                            MenuManager.SetDefaultSelection(0);
                        }
                        break;
                    case MenuSubState.ACTIVE:
                        UIManager.ControllerMenu();
                        Controls.m_disable_menu_letter_keys = false;
                        int menu_micro_state = MenuManager.m_menu_micro_state;

                        if (m_menu_state_timer > 0.25f)
                        {
                            if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir() || UIManager.SliderMouseDown()))
                            {
                                MenuManager.MaybeReverseOption();
                                MenuManager.m_calibration_step = -1;
                                Controller controller2 = Controls.m_controllers[MenuManager.m_calibration_current_controller];
                                switch (UIManager.m_menu_selection)
                                {
                                   case 233:     // set linear button
                                       MenuManager.PlaySelectSound(1f);
                                       if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                       {
                                            Vector2 start = ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[0];
                                            Vector2 end = ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[3];
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[1] = new Vector2(0.25f, start.y + 0.25f * (end.y - start.y));
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[2] = new Vector2(0.75f, start.y + 0.75f * (end.y - start.y));
                                        }
                                         break;
                                   case 234:     // reset curve button
                                        MenuManager.PlaySelectSound(1f);
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[0] = new Vector2(0, 0f);
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[1] = new Vector2(0.25f, 0.25f);
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[2] = new Vector2(0.75f, 0.75f);
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[3] = new Vector2(1f, 1f);
                                        }
                                         break;
                                    case 235: // apply to all axis
                                        MenuManager.PlaySelectSound(1f);
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
                                            for( int i = 0; i < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count; i++)
                                            {
                                                if( ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes.Count > i )
                                                {
                                                    ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[i].curve_points =  ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].CloneCurvePoints();
                                                    ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[i].curve_lookup =  ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
                                                }
                                                else
                                                {
                                                    Debug.Log("Error while pressing JoystickCurveEditor->[Apply to all Axis] Button: Axis mismatch between Overload.Controls and ExtendedConfig.Section_JoystickCurve");
                                                }
                                            }
                                        }
                                        break;
                                    case 100:
                                        MenuManager.PlaySelectSound(1f);
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
                                        }
                                        m_menu_state_timer = 0f;
                                        UIManager.DestroyAll(false);
                                        MenuManager.m_menu_state = 0;
                                        MenuManager.m_menu_micro_state = 0;
                                        MenuManager.m_menu_sub_state = MenuSubState.BACK;
                                        break;
                                }
                            }

                        }
                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                        {
                            ExtendedConfig.Section_JoystickCurve.Controller.Axis axis = ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis];
                            Vector2 initial_pos = new Vector2(0, -22f);
                            int xrange = 500;
                            int yrange = 500;

                            int limitx = xrange / 2;
                            int limity = yrange / 2;
                            initial_pos.x -= limitx;
                            initial_pos.y += limity;
                            if (move_point > -1 && move_point < 4) // if a valid point got selected and should be moved
                            {
                                if (Input.GetMouseButton(0)) // left mouse button still pressed ?
                                {
                                    Vector2 mouse_pos = UIManager.m_mouse_pos;
                                    mouse_pos -= initial_pos;
                                    if (!TestMouseInRect(new Vector2(0, -22f), 250f, 250f)) // make sure that the point boundaries are respected
                                    {
                                        mouse_pos.x = mouse_pos.x > xrange ? xrange : mouse_pos.x < 0 ? 0 : mouse_pos.x;
                                        mouse_pos.y = mouse_pos.y > 0 ? 0 : mouse_pos.y < -yrange ? -yrange : mouse_pos.y;
                                    }

                                    if (move_point == 0 || move_point == 3)
                                    {
                                        axis.curve_points[move_point].x = move_point == 0 ? 0f : 1f;
                                        axis.curve_points[move_point].y = -(mouse_pos.y / yrange);
                                    }
                                    else
                                    {
                                        axis.curve_points[move_point].x = mouse_pos.x / xrange;
                                        axis.curve_points[move_point].y = -(mouse_pos.y / yrange);
                                    }
                                }
                                else
                                {
                                    move_point = -1;
                                }
                            }
                            // otherwise test if a point should be selected
                            else
                            {
                                Vector2 point_pos = initial_pos;
                                int point_candidate = -1;
                                for (int i = 0; i < 4; i++)
                                {
                                    point_pos = initial_pos;
                                    point_pos.x += axis.curve_points[i].x * xrange; // get the current point position
                                    point_pos.y -= axis.curve_points[i].y * yrange;
                                    if (TestMouseInRect(point_pos, 20f, 20f)) point_candidate = i; // formerly 15
                                }
                                if (Input.GetMouseButton(0) && point_candidate != -1) // left mouse button
                                {
                                    move_point = point_candidate;
                                }
                            }

                        }


                        break;
                    case MenuSubState.BACK:
                        if (m_menu_state_timer > 0.25f)
                        {
                            MenuManager.ChangeMenuState(((Stack<MenuState>)AccessTools.Field(typeof(MenuManager), "m_back_stack").GetValue(null)).Pop(), true);
                            AccessTools.Field(typeof(MenuManager), "m_went_back").SetValue(null, true);
                        }
                        break;
                    case MenuSubState.START:
                        if (m_menu_state_timer > 0.25f)
                        {

                        }
                        break;
                }
            }

            public static bool TestMouseInRect(Vector2 pos, float x, float y)
            {
                if (!GameplayManager.VRActive)
                {
                    Rect rect = new Rect(pos.x - x, pos.y - y, x * 2f, y * 2f);
                    if (rect.Contains(UIManager.m_mouse_pos) || !UIManager.m_menu_use_mouse)
                    {
                        return true;
                    }
                }
                return false;

            }

        }


        // draws the curve editor
        [HarmonyPatch(typeof(UIElement), "Draw")]
        class JoystickCurveEditor_UIElement_Draw
        {
            static void Postfix(UIElement __instance)
            {
                if (__instance.m_type == Menus.uiAxisCurveEditor && __instance.m_alpha > 0f)
                    DrawCurveEditorWindow(__instance);
            }

            static void DrawCurveEditorWindow(UIElement uie)
            {
                UIManager.ui_bg_dark = true;
                uie.DrawMenuBG();
                Vector2 position = uie.m_position;
                position.y = UIManager.UI_TOP + 64f;
                uie.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 30f), Loc.LS("CURVE EDITOR"), 265f);
                position.y += 20f;
                uie.DrawMenuSeparator(position);
                position.y += 40f;

                Controller controller = Controls.m_controllers[MenuManager.m_calibration_current_controller];
                if (controller.isConnected)
                {
                    Vector2 pos = new Vector2(-605f, -270f);
                    uie.DrawStringSmall("CONTROLLER:", pos, 0.32f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                    pos.x += 95f;
                    uie.DrawStringSmall(controller.name, pos, 0.45f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                    pos.y += 42f;
                    pos.x -= 95f;
                    uie.DrawStringSmall("AXIS:", pos, 0.32f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);
                    pos.x += 95f;
                    uie.DrawStringSmall(MenuManager.m_calibration_current_axis + ": [" + Loc.LSX(controller.m_joystick.AxisElementIdentifiers[MenuManager.m_calibration_current_axis].name + "]"), pos, 0.45f, StringOffset.LEFT, UIManager.m_col_ui1, 1f, -1f);

                    Vector2 initial_pos = new Vector2(0, -22);
                    int xrange = 500;
                    int yrange = 500;

                    DrawStatsAxes(uie, initial_pos, xrange, yrange);

                    DrawResponseCurve(initial_pos, xrange, yrange);

                    pos.x += 960f;
                    pos.y += 334f;
                    uie.SelectAndDrawItem(Loc.LS("RESET CURVE"), pos, 234, false, 0.47f, 0.6f);
                    pos.y += 52f;
                    uie.SelectAndDrawItem(Loc.LS("SET TO LINEAR"), pos, 233, false, 0.47f, 0.6f);
                    pos.y += 52f;
                    uie.SelectAndDrawItem(Loc.LS("APPLY TO ALL AXES"), pos, 235, false, 0.47f, 0.6f);
                }

                position.y = UIManager.UI_BOTTOM - 120f;
                uie.DrawMenuSeparator(position);
                position.y += 5f;
                position.y = UIManager.UI_BOTTOM - 30f;
                uie.SelectAndDrawItem(Loc.LS("BACK"), position, 100, fade: false);
            }


            private static void DrawStatsAxes(UIElement __instance, Vector2 initial_pos, int xrange, int yrange)
            {
                float qyrange = yrange / 4;
                float qxrange = xrange / 4;
                Vector2 zero = initial_pos;
                Color c = UIManager.m_col_ub2;
                c.a = 1f * 0.75f;
                zero.y -= qyrange;
                UIManager.DrawQuadBarHorizontal(zero, 1f, 1f, xrange, c, 4);
                zero.y += qyrange;
                UIManager.DrawQuadBarHorizontal(zero, 1f, 1f, xrange, c, 4);
                zero.y += qyrange;
                UIManager.DrawQuadBarHorizontal(zero, 1f, 1f, xrange, c, 4);
                zero.y = initial_pos.y;

                zero.x -= qxrange;
                UIManager.DrawQuadBarVertical(zero, 1f, 1f, yrange, c, 4);
                zero.x += qxrange;
                UIManager.DrawQuadBarVertical(zero, 1f, 1f, yrange, c, 4);
                zero.x += qxrange;
                UIManager.DrawQuadBarVertical(zero, 1f, 1f, yrange, c, 4);

                zero.x = initial_pos.x;
                UIManager.DrawFrameEmptyCenter(zero, 4f, 4f, xrange - 2, yrange - 2, c, 8);
                c = UIManager.m_col_ui0;
                c.a = 0.8f;

                zero = initial_pos;
                zero.x += (yrange / 2) + 15;
                zero.y -= (yrange / 2) + 15;
                __instance.DrawStringSmall("[1,1]", zero, 0.4f, StringOffset.RIGHT, UIManager.m_col_ui0, 1f, -1f);
                zero.x -= yrange + 35;
                zero.y += yrange + 30;
                __instance.DrawStringSmall("[0,0]", zero, 0.4f, StringOffset.LEFT, UIManager.m_col_ui0, 1f, -1f);
            }

            private static void DrawResponseCurve(Vector2 initial_pos, int xrange, int yrange)
            {
                int cv = 6222419;
                Color color = new Color((cv >> 16) / 255f, ((cv >> 8) & 0xff) / 255f, (cv & 0xff) / 255f);

                initial_pos.x -= xrange / 2;
                initial_pos.y += yrange / 2;

                if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                {
                    ExtendedConfig.Section_JoystickCurve.Controller.Axis axis = ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis];

                    // draw the curve
                    Vector2 start = new Vector2(initial_pos.x, initial_pos.y - axis.curve_points[0].y * yrange);
                    Vector2 end = new Vector2(initial_pos.x + 1 * xrange, initial_pos.y - axis.curve_points[3].y * yrange);
                    for (float i = 0.02f; i <= 1f; i += 0.02f)
                    {
                        end.x = initial_pos.x + CubicBezierAxisForT(i, axis.curve_points[0].x, axis.curve_points[1].x, axis.curve_points[2].x, axis.curve_points[3].x) * xrange;
                        end.y = initial_pos.y - CubicBezierAxisForT(i, axis.curve_points[0].y, axis.curve_points[1].y, axis.curve_points[2].y, axis.curve_points[3].y) * yrange;
                        UIManager.DrawQuadCenterLine(start, end, 1f, 0f, color, 4);
                        start = end;
                    }

                    // draw deadzone
                    //UIManager.DrawQuadCenterLine(initial_pos, new Vector2(initial_pos.x + (Controllers.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].deadzone / 200f) * xrange, initial_pos.y), 1f, 0f, Color.red, 4);

                    // draw the lines from p0->p1, p2->p3
                    start = initial_pos;
                    start.x += axis.curve_points[0].x * xrange;
                    start.y -= axis.curve_points[0].y * yrange;
                    end = initial_pos;
                    end.x += axis.curve_points[1].x * xrange;
                    end.y -= axis.curve_points[1].y * yrange;
                    Vector2 local_start = start, local_end = start;
                    for (int i = 0; i < 20; i++)
                    {
                        local_end = local_start + 4 * ((end - start) / (20 * 7));
                        UIManager.DrawQuadCenterLine(local_start, local_end, 0.7f, 0f, new Color(255, 204, 153), 4);
                        local_start = local_end + 3 * ((end - start) / (20 * 7));
                    }
                    start = initial_pos;
                    end = initial_pos;
                    start.x += axis.curve_points[2].x * xrange;
                    start.y -= axis.curve_points[2].y * yrange;

                    end.x += axis.curve_points[3].x * xrange;
                    end.y -= axis.curve_points[3].y * yrange;
                    local_start = start;
                    local_end = start;
                    for (int i = 0; i < 20; i++)
                    {
                        local_end = local_start + 4 * ((end - start) / (20 * 7));
                        UIManager.DrawQuadCenterLine(local_start, local_end, 0.7f, 0f, new Color(255, 204, 153), 4);
                        local_start = local_end + 3 * ((end - start) / (20 * 7));
                    }


                    // draw blocks around the points
                    float radius = 7.5f;
                    for (int i = 0; i < 4; i++)
                    {
                        start = initial_pos;
                        start.x += axis.curve_points[i].x * xrange;
                        start.y -= axis.curve_points[i].y * yrange;
                        end = start;
                        start.x -= radius;
                        start.y += radius;
                        end.x -= radius;
                        end.y -= radius;
                        UIManager.DrawQuadCenterLine(start, end, 1f, 0f, Color.yellow, 4);
                        end.x += 2 * radius;
                        end.y += 2 * radius;
                        UIManager.DrawQuadCenterLine(start, end, 1f, 0f, Color.yellow, 4);
                        start.x += 2 * radius;
                        start.y -= 2 * radius;
                        UIManager.DrawQuadCenterLine(start, end, 1f, 0f, Color.yellow, 4);
                        end.x -= 2 * radius;
                        end.y -= 2 * radius;
                        UIManager.DrawQuadCenterLine(start, end, 1f, 0f, Color.yellow, 4);

                    }
                }
            }

        }


        // returns the blended value for t given 4 influencing points along one axis
        public static float CubicBezierAxisForT(float t, float a0, float a1, float a2, float a3)
        {
            return (float)(a0 * Math.Pow((1 - t), 3) + a1 * 3 * t * Math.Pow((1 - t), 2) + a2 * 3 * Math.Pow(t, 2) * (1 - t) + a3 * Math.Pow(t, 3));
        }


        



        // apply the curve 
        [HarmonyPatch(typeof(Controller), "GetAxis")]
        class JoystickCurveEditor_OverloadController_GetAxis
        {
            static bool Prefix(ref float __result, Controller __instance, int controller_num, int control_num)
            {
                if( string.IsNullOrEmpty(PilotManager.ActivePilot) )
                {
                    return true;
                }

                float axis_value = __instance.m_joystick.GetAxis(control_num);
                bool neg = false;
                if (axis_value < 0f)
                {
                    axis_value = axis_value * -1f;
                    neg = true;
                }
                float result = axis_value;

                try
                {
                    ExtendedConfig.Section_JoystickCurve.Controller.Axis a = ExtendedConfig.Section_JoystickCurve.controllers[controller_num].axes[control_num];
                    if (axis_value > Controllers.controllers[controller_num].axes[control_num].deadzone / 200f)
                    {
                        int i = (int)(axis_value / 0.005f);

                        if (i == 0)
                        {
                            result = axis_value / 0.005f * a.curve_lookup[0];
                        }
                        else if (i == 200)
                        {
                            result = a.curve_lookup[199] + ((axis_value - 0.995f) / 0.005f * (1f - a.curve_lookup[199]));
                        }
                        else
                        {
                            result = a.curve_lookup[i - 1] + ((axis_value - (i - 1) * 0.005f) / 0.005f * (a.curve_lookup[i] - a.curve_lookup[i - 1]));
                        }
                    }
                }
                catch( Exception ex )
                {
                    Debug.Log(" JoystickCurveEditor_OverloadController_GetAxis: Incorrect Device information: "+ex);
                    ExtendedConfig.Section_JoystickCurve.SetDefault();
                    return true;
                }

                if (axis_value > 0.5f)
                {
                    TemplateType template_type = Controls.m_controllers[controller_num].m_template_type;
                    if (template_type != TemplateType.Gamepad)
                    {
                        if (template_type == TemplateType.HOTAS)
                        {
                            if (Controls.m_last_primary_fire_time + 0.25f < GameplayManager.m_game_time)
                            {
                                Controls.m_last_primary_fire_time = GameplayManager.m_game_time;
                                Controls.m_controller_used_count[3]++;
                            }
                        }
                    }
                    else if (Controls.m_last_primary_fire_time + 0.25f < GameplayManager.m_game_time)
                    {
                        Controls.m_last_primary_fire_time = GameplayManager.m_game_time;
                        Controls.m_controller_used_count[2]++;
                    }
                }
                __result = result * (neg ? -1f : 1f);

                /*
                if (control_num < 100 && control_num > -1)
                {
                    DebugOutput.axes[control_num] = new DebugOutput.InputAdjustment
                    {
                        controller_num = controller_num,
                        control_num = control_num,
                        last_original_input = axis_value,
                        last_adjusted_input = result
                    };
                }*/
        
                return false;

            }
        }
        

    }
}
