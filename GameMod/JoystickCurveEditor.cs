using HarmonyLib;
using Overload;
using System;
using System.Collections.Generic;
using System.Globalization;
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
                if ((UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()) || UIManager.SliderMouseDown()) && MenuManager.m_menu_micro_state == 6)
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

        private static int last_menu_selection_id = -1;
        private static int currently_edited_index = -1;
        private static string currently_edited_value = "";

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
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
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
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
                                        }
                                        break;
                                    case 235: // apply to all axis
                                        MenuManager.PlaySelectSound(1f);
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
                                            for (int i = 0; i < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count; i++)
                                            {
                                                if (ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes.Count > i)
                                                {
                                                    ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[i].curve_points = ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].CloneCurvePoints();
                                                    ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[i].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
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
                            else
                            {
                                switch (UIManager.m_menu_selection)
                                {
                                    case 237:
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count 
                                            && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            if(last_menu_selection_id != UIManager.m_menu_selection)
                                            {
                                                last_menu_selection_id = UIManager.m_menu_selection;
                                                currently_edited_index = 0;
                                                currently_edited_value = FloatToString(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[0].y);
                                            }
                                            ApplyInput();
                                        }
                                        break;
                                    case 238:
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count
                                            && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            if (last_menu_selection_id != UIManager.m_menu_selection)
                                            {
                                                last_menu_selection_id = UIManager.m_menu_selection;
                                                currently_edited_index = 1;
                                                currently_edited_value = FloatToString(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[1].x);
                                            }
                                            ApplyInput();
                                        }
                                        break;
                                    case 239:
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count
                                            && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            if (last_menu_selection_id != UIManager.m_menu_selection)
                                            {
                                                last_menu_selection_id = UIManager.m_menu_selection;
                                                currently_edited_index = 2;
                                                currently_edited_value = FloatToString(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[1].y);
                                            }
                                            ApplyInput();
                                        }
                                        break;
                                    case 240:
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count
                                            && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            if (last_menu_selection_id != UIManager.m_menu_selection)
                                            {
                                                last_menu_selection_id = UIManager.m_menu_selection;
                                                currently_edited_index = 3;
                                                currently_edited_value = FloatToString(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[2].x);
                                            }
                                            ApplyInput();
                                        }
                                        break;
                                    case 241:
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count
                                            && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            if (last_menu_selection_id != UIManager.m_menu_selection)
                                            {
                                                last_menu_selection_id = UIManager.m_menu_selection;
                                                currently_edited_index = 4;
                                                currently_edited_value = FloatToString(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[2].y);
                                            }
                                            ApplyInput();
                                        }
                                        break;
                                    case 242:
                                        if (MenuManager.m_calibration_current_controller < Controllers.controllers.Count
                                            && MenuManager.m_calibration_current_axis < Controllers.controllers[MenuManager.m_calibration_current_controller].axes.Count)
                                        {
                                            if (last_menu_selection_id != UIManager.m_menu_selection)
                                            {
                                                last_menu_selection_id = UIManager.m_menu_selection;
                                                currently_edited_index = 5;
                                                currently_edited_value = FloatToString(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[3].y);
                                            }
                                            ApplyInput();
                                        }
                                        break;
                                }
                            }
                            if (last_menu_selection_id != UIManager.m_menu_selection && last_menu_selection_id >= 237 && last_menu_selection_id <= 242)
                            {
                                /*
                                if (float.TryParse(currently_edited_value, out float value_to_save))
                                {
                                    // save the edited value to the correct axis
                                    SaveValueToPoint(currently_edited_index, value_to_save);
                                }*/

                                // exit the editing state
                                currently_edited_index = -1;
                                currently_edited_value = "";
                            }
                            last_menu_selection_id = UIManager.m_menu_selection;
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
                                    ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
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

            private static void SaveValueToPoint(int index, float value_to_save)
            {
                //uConsole.Log("Save got called!");

                if (index < 0 || index > 5 || value_to_save < 0 || value_to_save > 1)
                    return;

                //uConsole.Log(" Save!!! part2: index: "+index+", val:"+value_to_save);
                switch (index)
                {
                    case 0:
                        ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[0].y = value_to_save;
                        break;
                    case 1:
                        ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[1].x = value_to_save;
                        break;
                    case 2:
                        ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[1].y = value_to_save;
                        break;
                    case 3:
                        ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[2].x = value_to_save;
                        break;
                    case 4:
                        ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[2].y = value_to_save;
                        break;
                    case 5:
                        ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points[3].y = value_to_save;
                        break;
                }
                ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_lookup = ExtendedConfig.Section_JoystickCurve.GenerateCurveLookupTable(ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points);
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



            private static void ApplyInput()
            {
                string tmp_input_holder = currently_edited_value;
                Controls.m_disable_menu_letter_keys = false;
                if ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V))
                    tmp_input_holder = GUIUtility.systemCopyBuffer;
                Controls.m_disable_menu_letter_keys = true;
                
                ProcessInputField(ref tmp_input_holder);

                // validate result
                if (float.TryParse(tmp_input_holder, out float result))
                {
                    if (result >= 0f & result <= 1f)
                    {
                        currently_edited_value = tmp_input_holder;
                        SaveValueToPoint(currently_edited_index, result);
                    }
                }
                else if (string.IsNullOrEmpty(tmp_input_holder))
                    currently_edited_value = tmp_input_holder;

                //uConsole.Log("select: " + UIManager.m_menu_selection + ",  current value: " + currently_edited_value+ ",  idx: "+currently_edited_index);
                        
            }

            private static void ProcessInputField(ref string s)
            {
                foreach (char c in Input.inputString)
                {
                    if (c == '\b')
                    {
                        if (s.Length != 0)
                        {
                            s = s.Substring(0, s.Length - 1);
                            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_bot_died, 0.4f, UnityEngine.Random.Range(-0.15f, -0.05f), 0f, false);
                        }
                    }
                    else
                    {
                        if (c == '\n' || c == '\r')
                        {
                            s = s.Trim();
                            MenuManager.SetDefaultSelection((MenuManager.m_menu_micro_state != 1) ? 1 : 0);
                            MenuManager.PlayCycleSound(1f, 1f);
                        }
                        if (MenuManager.IsPrintableChar(c) && s.Length < 6 && c != ' ' && ((c >= '0' && c <= '9') || c == ',' || c == '.'))
                        {
                            if (c == ',')
                                s += ".";
                            else
                                s += c;
                            SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_bot_died, 0.5f, UnityEngine.Random.Range(0.1f, 0.2f), 0f, false);
                        }
                    }
                }
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

                    DrawResponseCurve(uie, initial_pos, xrange, yrange);

                    uie.DrawStringSmall("Horizontal", new Vector2(437, -148), 0.35f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, -1f);
                    uie.DrawStringSmall("Vertical", new Vector2(530, -148), 0.35f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, -1f);

                    pos.x += 960f;
                    Vector2 pos2 = pos;
                    Vector2[] curve_points = ExtendedConfig.Section_JoystickCurve.controllers[MenuManager.m_calibration_current_controller].axes[MenuManager.m_calibration_current_axis].curve_points;
                    pos2.x -= 10f;
                    pos2.y += 100f;
                    DrawDumbTextEntry(uie, pos2, FloatToString(curve_points[0].x), "POINT 1:");
                    pos2.x += 92f;
                    DrawTextEntry(uie, pos2, 237, currently_edited_index == 0 ? currently_edited_value : (FloatToString(curve_points[0].y).Length > 6 ? FloatToString(curve_points[0].y).Substring(0, 6) : FloatToString(curve_points[0].y)));
                    pos2.x -= 92f;
                    pos2.y += 33f;
                    DrawTextEntry(uie, pos2, 238, currently_edited_index == 1 ? currently_edited_value : (FloatToString(curve_points[1].x).Length > 6 ? FloatToString(curve_points[1].x).Substring(0, 6) : FloatToString(curve_points[1].x)), "POINT 2:");
                    pos2.x += 92f;
                    DrawTextEntry(uie, pos2, 239, currently_edited_index == 2 ? currently_edited_value : (FloatToString(curve_points[1].y).Length > 6 ? FloatToString(curve_points[1].y).Substring(0, 6) : FloatToString(curve_points[1].y)));
                    pos2.x -= 92f;
                    pos2.y += 33f;
                    DrawTextEntry(uie, pos2, 240, currently_edited_index == 3 ? currently_edited_value : (FloatToString(curve_points[2].x).Length > 6 ? FloatToString(curve_points[2].x).Substring(0, 6) : FloatToString(curve_points[2].x)), "POINT 3:");
                    pos2.x += 92f;
                    DrawTextEntry(uie, pos2, 241, currently_edited_index == 4 ? currently_edited_value : (FloatToString(curve_points[2].y).Length > 6 ? FloatToString(curve_points[2].y).Substring(0, 6) : FloatToString(curve_points[2].y)));
                    pos2.x -= 92f;
                    pos2.y += 33f;
                    DrawDumbTextEntry(uie, pos2, FloatToString(curve_points[3].x), "POINT 4:");
                    pos2.x += 92f;
                    DrawTextEntry(uie, pos2, 242, currently_edited_index == 5 ? currently_edited_value : (FloatToString(curve_points[3].y).Length > 6 ? FloatToString(curve_points[3].y).Substring(0, 6) : FloatToString(curve_points[3].y)));


                    pos.y += 334f;
                    uie.SelectAndDrawItem(Loc.LS("RESET CURVE"), pos, 234, false, 0.47f, 0.6f);
                    pos.y += 52f;
                    uie.SelectAndDrawItem(Loc.LS("SET TO LINEAR"), pos, 233, false, 0.47f, 0.6f);
                    pos.y += 52f;
                    uie.SelectAndDrawItem(Loc.LS("APPLY TO ALL AXES"), pos, 235, false, 0.47f, 0.6f);
                }

                position.y = UIManager.UI_BOTTOM - 120f;
                uie.DrawMenuSeparator(position);
                position.y += 25f;
                uie.DrawStringSmall("AXIS DEFLECTION", position, 0.4f, StringOffset.CENTER, UIManager.m_col_ui0, 1f, -1f);
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

            public static bool init = false;

            private static void DrawResponseCurve(UIElement __instance, Vector2 initial_pos, int xrange, int yrange)
            {
                int cv = 6222419;
                Color color = new Color((cv >> 16) / 255f, ((cv >> 8) & 0xff) / 255f, (cv & 0xff) / 255f);
                int controller_num = MenuManager.m_calibration_current_controller;
                int control_num = MenuManager.m_calibration_current_axis;

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

                    // draw an indicator for current input on this axis to get a better idea of what movements correspond to what curve points
                    if (!(Controls.m_controllers.Count <= controller_num) & Controls.m_controllers[controller_num].isConnected)
                    {
                        float axis_raw_value = Controls.m_controllers[controller_num].m_joystick.GetAxis(control_num);
                        //float axis_altered_value = Controls.m_controllers[controller_num].GetAxis(MenuManager.m_calibration_current_axis, controller_num);
                        
                        float axis_value = axis_raw_value;
                        if (axis_value < 0f)
                        {
                            axis_value *= -1f;
                        }
                        float axis_altered_value = axis_value;
                        ExtendedConfig.Section_JoystickCurve.Controller.Axis a = ExtendedConfig.Section_JoystickCurve.controllers[controller_num].axes[control_num];
                        int x = (int)(axis_value / 0.005f);
                        if (x == 0)
                        {
                            axis_altered_value = axis_value / 0.005f * a.curve_lookup[1];
                        }
                        else if (x == 200)
                        {
                            axis_altered_value = Mathf.Clamp(a.curve_lookup[199] + ((axis_value - 0.995f) / 0.005f * (a.curve_lookup[199] - a.curve_lookup[198])), 0f, 1f);
                        }
                        else
                        {
                            axis_altered_value = a.curve_lookup[x - 1] + ((axis_value - (x - 1) * 0.005f) / 0.005f * (a.curve_lookup[x] - a.curve_lookup[x - 1]));
                        }

                        if (axis_raw_value < 0f) axis_raw_value *= -1f;
                        //if (axis_altered_value < 0f) axis_altered_value *= -1f;

                        Vector2 start_position = new Vector2(initial_pos.x + xrange * axis_raw_value, initial_pos.y);
                        Vector2 end_position = new Vector2(initial_pos.x + xrange * axis_raw_value, initial_pos.y - axis_altered_value * yrange);
                        UIManager.DrawQuadCenterLine(start_position, end_position, 1f, 0f, Color.yellow, 1);
                        __instance.DrawStringSmall("[" + axis_raw_value.ToString("n3") + " , " + axis_altered_value.ToString("n3") + "]",
                            new Vector2(initial_pos.x + xrange * axis_raw_value, initial_pos.y + 27f), 0.4f, StringOffset.CENTER, Color.yellow, 1f, -1f);
                    }


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

            public static void DrawTextEntry(UIElement uie, Vector2 pos, int idx, string text, string label = "")
            {
                if (!uie.m_fade_die)
                {
                    uie.TestMouseInRect(pos, 50f, 17f, idx, true);
                }
                bool flag = UIManager.m_menu_selection == idx;
                pos.y += 5f;
                uie.DrawWideBox(pos, 32f, 13f, (!flag) ? UIManager.m_col_ub0 : UIManager.m_col_ui2, uie.m_alpha, 7);

                uie.DrawStringSmall(text, pos, 0.45f, StringOffset.CENTER, (!flag) ? UIManager.m_col_ub1 : UIManager.m_col_ui5, 1f, -1f);
                pos.x -= 100f;
                if (!string.IsNullOrEmpty(label))
                {
                    uie.DrawStringSmall(label, pos, 0.4f, StringOffset.CENTER, UIManager.m_col_ub0, 1f, -1f);
                }

            }

            public static void DrawDumbTextEntry(UIElement uie, Vector2 pos, string text, string label = "")
            {
                pos.y += 5f;
                uie.DrawWideBox(pos, 32f, 13f, Color.gray * 0.35f, uie.m_alpha, 7); //UIManager.m_col_ub0

                uie.DrawStringSmall(text, pos, 0.45f, StringOffset.CENTER, Color.gray * 0.35f, 1f, -1f); //UIManager.m_col_ub1 * 0.6f
                pos.x -= 100f;
                if (!string.IsNullOrEmpty(label))
                {
                    uie.DrawStringSmall(label, pos, 0.4f, StringOffset.CENTER, UIManager.m_col_ub0, 1f, -1f);
                }

            }

  

        }

        public static string FloatToString(float f)
        {
            string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            string strdec = f.ToString(CultureInfo.CurrentCulture);
            return strdec.Contains(sep) ? strdec.TrimEnd('0').TrimEnd(sep.ToCharArray()) : strdec;
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
                if (string.IsNullOrEmpty(PilotManager.ActivePilot))
                {
                    return true;
                }
                //uConsole.Log(__instance.m_joystick.calibrationMap.GetAxis(control_num).calibratedMin + " , " + __instance.m_joystick.calibrationMap.GetAxis(control_num).calibratedZero + " , " + __instance.m_joystick.calibrationMap.GetAxis(control_num).calibratedMax);
                /*
                float calibrated_minimum = __instance.GetAxisMin(control_num);
                float calibrated_zero = __instance.GetAxisZero(control_num);
                float calibrated_maximum = __instance.GetAxisMax(control_num);



                float axis_value =__instance.m_joystick.GetAxis(control_num);
                float raw = axis_value;
                bool neg = false;
                if (axis_value < calibrated_zero)
                {
                    axis_value = (axis_value + calibrated_zero) / (calibrated_minimum + calibrated_zero);
                    axis_value *= -1f;
                    neg = true;
                }
                else
                {
                    axis_value = (axis_value - calibrated_zero) / (calibrated_maximum - calibrated_zero);
                }
                uConsole.Log("raw: " + raw + ",  cal: " + axis_value);*/
                float axis_value = __instance.m_joystick.GetAxis(control_num);

                bool neg = false;
                if (axis_value < 0f)
                {
                    axis_value *= -1f;
                    neg = true;
                }
                float result = axis_value;

                try
                {
                    ExtendedConfig.Section_JoystickCurve.Controller.Axis a = ExtendedConfig.Section_JoystickCurve.controllers[controller_num].axes[control_num];
                    //if (axis_value > Controllers.controllers[controller_num].axes[control_num].deadzone / 200f)
                    //{
                        int i = (int)(axis_value / 0.005f);

                        if (i == 0)
                        {
                            result = axis_value / 0.005f * a.curve_lookup[1];
                        }
                        else if (i == 200)
                        {
                            result = Mathf.Clamp(a.curve_lookup[199] + ((axis_value - 0.995f) / 0.005f * (a.curve_lookup[199] - a.curve_lookup[198])), 0f, 1f);
                            //result = a.curve_lookup[199] + ((axis_value - 0.995f) / 0.005f * (1f - a.curve_lookup[199]));
                        }
                        else
                        {
                            result = a.curve_lookup[i - 1] + ((axis_value - (i - 1) * 0.005f) / 0.005f * (a.curve_lookup[i] - a.curve_lookup[i - 1]));
                        }
                    //}
                }
                catch (Exception ex)
                {
                    Debug.Log(" JoystickCurveEditor_OverloadController_GetAxis: Incorrect Device information: " + ex);
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

                //DEBUG
                /*
                if (control_num < 100 && control_num > -1)
                {
                    int axis_index_to_save_to = controller_num * 16 + control_num;
                    if(axis_index_to_save_to < DebugOutput.axes.Length)
                    {
                        DebugOutput.axes[axis_index_to_save_to] = new DebugOutput.InputAdjustment
                        {
                            controller_num = controller_num,
                            control_num = control_num,
                            last_original_input = axis_value,
                            last_adjusted_input = result
                        };
                    }
                }
                */
                return false;

            }
        }


    }
}
