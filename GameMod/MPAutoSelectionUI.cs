using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using Harmony;
using Overload;
using UnityEngine;

namespace GameMod
{
    class MPAutoSelectionUI
    {
        // Adds the 'CONFIGURE AUTOSELECT' Option as the Entrypoint for the Autoselect menu under 'Options/Control Options/'
        [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
        internal class MPAutoSelectionUI_UIElement_DrawControlsMenu
        {
            private static void DrawAutoselectMenuOption(UIElement uie, ref Vector2 position)
            {
                uie.SelectAndDrawItem(Loc.LS("CONFIGURE AUTOSELECT"), position, 121, false, 1f, 0.75f);
                //position.y += 55f;
            }

            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var codes = new List<CodeInstruction>(instructions);
                int state = 0;
                for (int i = 0; i < codes.Count; i++)
                {

                    if (state == 0 && codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "CONTROL OPTIONS - ADVANCED")
                    {
                        // remove the 'PRIMARY AUTOSELECT' option
                        codes.RemoveRange(i + 17, 11);

                        // add the autoselect menu button
                        var newCodes = new[] {
                            new CodeInstruction(OpCodes.Ldarg_0),
                            new CodeInstruction(OpCodes.Ldloca, 0),
                            new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPAutoSelectionUI_UIElement_DrawControlsMenu), "DrawAutoselectMenuOption"))
                        };
                        codes.InsertRange(i + 17, newCodes);
                        state++;
                    }

                    // make some space
                    if (state > 0 && state < 10 && codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 62f)
                    {
                        codes[i].operand = 55f;
                    }

                    if (state < 10 && codes[i].opcode == OpCodes.Ldstr && (string)codes[i].operand == "INVERT SLIDE MODIFIER Y")
                    {
                        state = 10;
                    }
                }
                return codes;
            }
        }


        // Changes the menu state if the "CONFIGURE AUTOSELECT" Button gets pressed
        [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
        class MPAutoSelectionUI_MenuManager_ControlsOptionsUpdate
        {
            static void Postfix()
            {
                if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()) || UIManager.SliderMouseDown())
                {
                    switch (UIManager.m_menu_selection)
                    {
                        case 121:
                            MenuManager.ChangeMenuState(Menus.msAutoSelect, false);
                            UIManager.DestroyAll(false);
                            MenuManager.PlaySelectSound(1f);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        // Handles the menu logic of the added buttons
        [HarmonyPatch(typeof(MenuManager), "Update")]
        class MPAutoSelectionUI_MenuManager_Update
        {

            private static void Postfix(ref float ___m_menu_state_timer)
            {
                if (MenuManager.m_menu_state == Menus.msAutoSelect)
                    AutoSelectUpdate(ref ___m_menu_state_timer);
            }

            public static int selected;
            public static int selected2;
            private static void AutoSelectUpdate(ref float m_menu_state_timer)
            {
                selected = MPAutoSelectUI_UIElement_Draw.returnPrimarySelected();
                selected2 = MPAutoSelectUI_UIElement_Draw.returnSecondarySelected();
                UIManager.MouseSelectUpdate();
                switch (MenuManager.m_menu_sub_state)
                {
                    case MenuSubState.INIT:
                        if (m_menu_state_timer > 0.25f)
                        {
                            UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, Menus.uiAutoSelect);
                            MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                            m_menu_state_timer = 0f;
                            MenuManager.SetDefaultSelection(0);
                        }
                        break;

                    case MenuSubState.ACTIVE:
                        UIManager.ControllerMenu();
                        Controls.m_disable_menu_letter_keys = false;

                        if (m_menu_state_timer > 0.25f)
                        {
                            if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir() || UIManager.SliderMouseDown()))
                            {
                                MenuManager.MaybeReverseOption();
                                switch (UIManager.m_menu_selection)
                                {

                                    case 100:
                                        MenuManager.PlaySelectSound(1f);
                                        m_menu_state_timer = 0f;
                                        UIManager.DestroyAll(false);
                                        MenuManager.m_menu_state = 0;
                                        MenuManager.m_menu_micro_state = 0;
                                        MenuManager.m_menu_sub_state = MenuSubState.BACK;
                                        break;
                                    case 200:
                                    case 201:
                                    case 202:
                                    case 203:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            MenuManager.m_menu_micro_state = UIManager.m_menu_selection - 200;
                                            MenuManager.UIPulse(1f);
                                            GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                                        }
                                        break;

                                    // Triggers Swap Logic for the Primary Weapon Buttons
                                    case 1720:
                                    case 1721:
                                    case 1722:
                                    case 1723:
                                    case 1724:
                                    case 1725:
                                    case 1726:
                                    case 1727: // int nwhen (MenuManager.m_menu_micro_state > 1719 && MenuManager.m_menu_micro_state <= 1727):
                                        if (UIManager.PushedSelect(100)) doSelectedStuffForPrimary(UIManager.m_menu_selection - 1720);
                                        break;


                                    // Triggers Swap Logic for the Secondary Weapon Buttons
                                    case 1728:
                                    case 1729:
                                    case 1730:
                                    case 1731:
                                    case 1732:
                                    case 1733:
                                    case 1734:
                                    case 1735:
                                        if (UIManager.PushedSelect(100)) doSelectedStuffForSecondary(UIManager.m_menu_selection - 1728);
                                        break;

                                    // Triggers Neverselect Logic for the Primary Buttons
                                    case 2000:
                                    case 2001:
                                    case 2002:
                                    case 2003:
                                    case 2004:
                                    case 2005:
                                    case 2006:
                                    case 2007:
                                        if (UIManager.PushedSelect(100)) doNeverSelectStuffForPrimary(UIManager.m_menu_selection - 2000);
                                        break;

                                    // Triggers Neverselect Logic for the Secondary Buttons
                                    case 2010:
                                    case 2011:
                                    case 2012:
                                    case 2013:
                                    case 2014:
                                    case 2015:
                                    case 2016:
                                    case 2017:
                                        if (UIManager.PushedSelect(100)) doNeverSelectStuffForSecondary(UIManager.m_menu_selection - 2010);
                                        break;

                                    case 2100:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            if (MPAutoSelection.primarySwapFlag || MPAutoSelection.secondarySwapFlag)
                                            {
                                                MPAutoSelection.primarySwapFlag = false;
                                                MPAutoSelection.secondarySwapFlag = false;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                                            }
                                            else
                                            {
                                                MPAutoSelection.primarySwapFlag = true;
                                                MPAutoSelection.secondarySwapFlag = true;
                                                MenuManager.opt_primary_autoswitch = 0;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                                            }
                                            ExtendedConfig.Section_AutoSelect.Set(true);
                                        }
                                        break;
                                    case 2102:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            if (MPAutoSelection.secondarySwapFlag)
                                            {
                                                MPAutoSelection.secondarySwapFlag = false;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                                            }
                                            else
                                            {
                                                MPAutoSelection.secondarySwapFlag = true;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                                            }
                                            ExtendedConfig.Section_AutoSelect.Set(true);
                                        }
                                        break;
                                    case 2103:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            if (MPAutoSelection.primarySwapFlag)
                                            {
                                                MPAutoSelection.primarySwapFlag = false;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                                            }
                                            else
                                            {
                                                MPAutoSelection.primarySwapFlag = true;
                                                MenuManager.opt_primary_autoswitch = 0;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                                            }
                                            ExtendedConfig.Section_AutoSelect.Set(true);
                                        }
                                        break;
                                    case 2104:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            if (MPAutoSelection.zorc)
                                            {
                                                MPAutoSelection.zorc = false;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                                            }
                                            else
                                            {
                                                MPAutoSelection.zorc = true;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                                            }
                                            ExtendedConfig.Section_AutoSelect.Set(true);
                                        }
                                        break;
                                    case 2105:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            if (MPAutoSelection.dontAutoselectAfterFiring)
                                            {
                                                MPAutoSelection.dontAutoselectAfterFiring = false;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                                            }
                                            else
                                            {
                                                MPAutoSelection.dontAutoselectAfterFiring = true;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                                            }
                                            ExtendedConfig.Section_AutoSelect.Set(true);
                                        }
                                        break;
                                    case 2106:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            if (MPAutoSelection.swapWhileFiring)
                                            {
                                                MPAutoSelection.swapWhileFiring = false;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                                            }
                                            else
                                            {
                                                MPAutoSelection.swapWhileFiring = true;
                                                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                                            }
                                            ExtendedConfig.Section_AutoSelect.Set(true);
                                        }
                                        break;
                                    case 2107:
                                        if (UIManager.PushedSelect(100))
                                        {
                                            SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                                            ExtendedConfig.Section_AutoSelect.Set();
                                            ExtendedConfig.Section_AutoSelect.ApplySettings();
                                        }
                                        break;
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


            private static void doNeverSelectStuffForPrimary(int i)
            {
                MPAutoSelection.PrimaryNeverSelect[i] = !MPAutoSelection.PrimaryNeverSelect[i];
                if (!MPAutoSelection.PrimaryNeverSelect[i])
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                }
                else
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                }
                ExtendedConfig.Section_AutoSelect.Set(true);
            }

            private static void doNeverSelectStuffForSecondary(int i)
            {
                MPAutoSelection.SecondaryNeverSelect[i] = !MPAutoSelection.SecondaryNeverSelect[i];
                if (!MPAutoSelection.SecondaryNeverSelect[i])
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
                }
                else
                {
                    SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
                }
                ExtendedConfig.Section_AutoSelect.Set(true);
            }

            private static void doSelectedStuffForPrimary(int i)
            {
                if (selected < 1)
                {
                    MPAutoSelectUI_UIElement_Draw.isPrimarySelected[i] = true;
                    GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                }
                else
                {
                    if (MPAutoSelectUI_UIElement_Draw.isPrimarySelected[i])
                    {
                        MPAutoSelectUI_UIElement_Draw.isPrimarySelected[i] = false;
                        GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                    }
                    else
                    {
                        MPAutoSelectUI_UIElement_Draw.isPrimarySelected[i] = true;
                        MPAutoSelectUI_UIElement_Draw.SwapSelectedPrimary();
                        SFXCueManager.PlayCue2D(SFXCue.guidebot_objective_found, 0.8f, 0f, 0f, false);
                    }
                }
            }

            private static void doSelectedStuffForSecondary(int i)
            {
                if (selected2 < 1)
                {
                    MPAutoSelectUI_UIElement_Draw.isSecondarySelected[i] = true;
                    GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                }
                else
                {
                    if (MPAutoSelectUI_UIElement_Draw.isSecondarySelected[i])
                    {
                        MPAutoSelectUI_UIElement_Draw.isSecondarySelected[i] = false;
                        GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                    }
                    else
                    {
                        MPAutoSelectUI_UIElement_Draw.isSecondarySelected[i] = true;
                        MPAutoSelectUI_UIElement_Draw.SwapSelectedSecondary();
                        SFXCueManager.PlayCue2D(SFXCue.guidebot_objective_found, 0.8f, 0f, 0f, false);
                    }
                }
            }

        }

        [HarmonyPatch(typeof(UIElement), "Draw")]
        public class MPAutoSelectUI_UIElement_Draw
        {
            static void Postfix(UIElement __instance)
            {
                if (__instance.m_type == Menus.uiAutoSelect)
                {
                    DrawAutoSelectWindow(__instance);
                }   
            }

            static string[] PrimaryPriorityArray = new string[8];
            static string[] SecondaryPriorityArray = new string[8];

            static void DrawAutoSelectWindow(UIElement uie)
            {
                UIManager.X_SCALE = 0.2f;
                UIManager.ui_bg_dark = true;
                uie.DrawMenuBG();

                Vector2 position = uie.m_position;
                position.y = UIManager.UI_TOP + 64f;
                uie.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 40f), Loc.LS("AUTOSELECT"), 265f);
                position.y += 100f;
                uie.DrawMenuSeparator(position);
                position.y -= 40f;




                position.y += 82;
                Vector2 position2 = position;
                position.x -= 160f;
                position2.x += 160f;

                UIColorPrimaries = MPAutoSelection.primarySwapFlag ? UIManager.m_col_ui4 : UIManager.m_col_ui0;
                UIColorSecondaries = MPAutoSelection.secondarySwapFlag ? UIManager.m_col_ui4 : UIManager.m_col_ub0;

                Vector2 left = position;
                Vector2 right = position;
                left.x += 81;
                right.x += 234;

                //Draw the neverselect Buttons
                for (int i = 0; i < 8; i++)
                {
                    int primaryindex = getWeaponIconIndex(MPAutoSelection.PrimaryPriorityArray[i]);
                    int secondaryindex = getWeaponIconIndex(MPAutoSelection.SecondaryPriorityArray[i]);
                    UIManager.DrawSpriteUI(left, 0.2f, 0.2f, UIColorPrimaries, uie.m_alpha, 26 + primaryindex);
                    UIManager.DrawSpriteUI(right, 0.2f, 0.2f, UIColorSecondaries, uie.m_alpha, 104 + secondaryindex);

                    left.y += 50f;
                    right.y += 50f;
                    if (MPAutoSelectUI_UIElement_Draw.isPrimarySelected[i])
                    {
                        uie.DrawWideBox(position, 100f, 28f, Color.blue, 0.2f, 7);
                        UIManager.DrawQuadBarHorizontal(position, 100f, 18f, 30f, Color.blue, 12);
                    }
                    else if (MPAutoSelection.PrimaryNeverSelect[i])
                    {
                        uie.DrawWideBox(position, 100f, 28f, Color.red, 0.2f, 7);
                        UIManager.DrawQuadBarHorizontal(position, 100f, 18f, 30f, Color.red, 12);
                    }
                    position.x -= 150f;
                    uie.SelectAndDrawItem(!MPAutoSelection.PrimaryNeverSelect[i] ? "+" : "-", position, 2000 + i, false, 0.022f, 1f);
                    position.x += 150f;
                    uie.SelectAndDrawHalfItem(MPAutoSelection.PrimaryPriorityArray[i], position, 1720 + i, false);
                    position.y += 50f;


                    if (MPAutoSelectUI_UIElement_Draw.isSecondarySelected[i])
                    {
                        uie.DrawWideBox(position2, 100f, 28f, Color.blue, 0.2f, 7);
                        UIManager.DrawQuadBarHorizontal(position2, 100f, 18f, 30, Color.blue, 12);
                    }
                    else if (MPAutoSelection.SecondaryNeverSelect[i])
                    {
                        uie.DrawWideBox(position2, 100f, 28f, Color.red, 0.2f, 7);
                        UIManager.DrawQuadBarHorizontal(position2, 100f, 18f, 30, Color.red, 12);
                    }
                    position2.x += 150f;
                    uie.SelectAndDrawItem((!MPAutoSelection.SecondaryNeverSelect[i] ? "+" : "-"), position2, 2010 + i, false, 0.022f, 1f);
                    position2.x -= 150f;
                    uie.SelectAndDrawHalfItem(MPAutoSelection.SecondaryPriorityArray[i], position2, 1728 + i, false);
                    position2.y += 50f;
                }



                position = left;
                position.x = 540f;
                position.y -= 400f;
                uie.SelectAndDrawItem("Status: " + ((MPAutoSelection.primarySwapFlag || MPAutoSelection.secondarySwapFlag) ? "ACTIVE" : "INACTIVE"), position, 2100, false, 0.3f, 0.45f);
                position.y += 50f;
                position.x += 5f;
                uie.SelectAndDrawItem("Weapon Logic: " + (MPAutoSelection.primarySwapFlag ? "ON" : "OFF"), position, 2103, false, 0.27f, 0.4f);
                position.y += 50f;
                uie.SelectAndDrawItem("Missile Logic: " + (MPAutoSelection.secondarySwapFlag ? "ON" : "OFF"), position, 2102, false, 0.27f, 0.4f);


                position.x -= 5f;
                position.y += 74f;
                uie.SelectAndDrawItem("DONT SWAP WHILE FIRING: " + (!MPAutoSelection.swapWhileFiring ? "ON" : "OFF"), position, 2106, false, 0.3f, 0.40f);
                position.y += 50f;
                uie.SelectAndDrawItem("RETRY SWAP AFTER FIRING: " + (!MPAutoSelection.dontAutoselectAfterFiring ? "ON" : "OFF"), position, 2105, false, 0.3f, 0.40f);
                position.y += 50f;
                uie.SelectAndDrawItem("ALERT: " + (MPAutoSelection.zorc ? "ON" : "OFF"), position, 2104, false, 0.3f, 0.45f);
                position.y += 73f;
                uie.SelectAndDrawItem("RESET TO DEFAULTS", position, 2107, false, 0.3f, 0.45f);



                // Button description 
                position2.x -= 160f;
                position2.y -= 8f;
                
                position2.y -= 8f;
                string k = selectionToDescription(UIManager.m_menu_selection);
                MPAutoSelection.last_valid_description = k;
                position.x = 0f;
                uie.DrawLabelSmall(position + Vector2.up * 40f, k, 500f); //position2
                position.y += 12f;
                uie.DrawMenuSeparator(position + Vector2.up * 40f);


                position.x = 0f;
                position.y = UIManager.UI_BOTTOM - 30f;
                uie.SelectAndDrawItem(Loc.LS("BACK"), position, 100, fade: false);
            }


            

            public static int returnPrimarySelected()
            {
                int counter = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (isPrimarySelected[i])
                    {
                        counter++;
                    }
                }
                return counter;
            }

            public static int returnSecondarySelected()
            {
                int counter = 0;
                for (int i = 0; i < 8; i++)
                {
                    if (isSecondarySelected[i])
                    {
                        counter++;
                    }
                }
                return counter;
            }

            public static void SwapSelectedPrimary()
            {
                int counter = 0;
                int[] selection = { 0, 0 };
                for (int i = 0; i < 8; i++)
                {
                    if (isPrimarySelected[i])
                    {
                        selection[counter] = i;
                        counter++;
                    }
                    if (counter == 2)
                    {
                        break;
                    }
                }
                string temp = MPAutoSelection.PrimaryPriorityArray[selection[0]];
                MPAutoSelection.PrimaryPriorityArray[selection[0]] = MPAutoSelection.PrimaryPriorityArray[selection[1]];
                MPAutoSelection.PrimaryPriorityArray[selection[1]] = temp;

                isPrimarySelected[selection[0]] = false;
                isPrimarySelected[selection[1]] = false;
                MPAutoSelection.PrimaryNeverSelect[selection[0]] = false;
                MPAutoSelection.PrimaryNeverSelect[selection[1]] = false;

                ExtendedConfig.Section_AutoSelect.Set(true);
                MPAutoSelection.Initialise();
            }

            public static void SwapSelectedSecondary()
            {
                int counter = 0;
                int[] selection = { 0, 0 };
                for (int i = 0; i < 8; i++)
                {
                    if (isSecondarySelected[i])
                    {
                        selection[counter] = i;
                        counter++;
                    }
                    if (counter == 2)
                    {
                        break;
                    }
                }
                string temp = MPAutoSelection.SecondaryPriorityArray[selection[0]];
                MPAutoSelection.SecondaryPriorityArray[selection[0]] = MPAutoSelection.SecondaryPriorityArray[selection[1]];
                MPAutoSelection.SecondaryPriorityArray[selection[1]] = temp;

                isSecondarySelected[selection[0]] = false;
                isSecondarySelected[selection[1]] = false;
                MPAutoSelection.SecondaryNeverSelect[selection[0]] = false;
                MPAutoSelection.SecondaryNeverSelect[selection[1]] = false;

                ExtendedConfig.Section_AutoSelect.Set(true);
                MPAutoSelection.Initialise();
            }


            public static string selectionToDescription(int n)
            {
                if (n == 2100) return "TOGGLES WETHER THE WHOLE MOD SHOULD BE ACTIVE";
                if (n == 2101) return "REPLACES THE `PREV/NEXT WEAPON` FUNCTION WITH `SWAP TO NEXT HIGHER/LOWER PRIORITIZED WEAPONS`";
                if (n <= 2017 && n >= 2000) return "TOGGLES WETHER THIS WEAPON SHOULD NEVER BE SELECTED";
                if (n <= 1735 && n >= 1720) return "CHANGE THE ORDER BY CLICKING AT THE TWO WEAPONS YOU WANT TO SWAP";
                if (n == 2103) return "TOGGLES EVERYTHING RELATED TO PRIMARY WEAPONS IN THIS MOD";
                if (n == 2102) return "TOGGLES EVERYTHING RELATED TO SECONDARY WEAPONS IN THIS MOD";
                if (n == 2104) return "DISPLAY A WARNING IF A DEVASTATOR GETS AUTOSELECTED";
                if (n == 2105) return "DELAY SWAPS TILL THE PLAYER IS NOT FIRING ANYMORE";
                if (n == 2106) return "SWAP EVEN IF THE PLAYER IS CURRENTLY FIRING";
                if (n == 2107) return "RESETS AUTOSELECT SETTINGS TO DEFAULTS FOR THIS PILOT";
                else {
                    return MPAutoSelection.last_valid_description;
                }
            }

            public static int getWeaponIconIndex(string weapon)
            {
                if (weapon.Equals("IMPULSE") || weapon.Equals("FALCON")) return 0;
                if (weapon.Equals("CYCLONE") || weapon.Equals("MISSILE_POD")) return 1;
                if (weapon.Equals("REFLEX") || weapon.Equals("HUNTER")) return 2;
                if (weapon.Equals("CRUSHER") || weapon.Equals("CREEPER")) return 3;
                if (weapon.Equals("DRILLER") || weapon.Equals("NOVA")) return 4;
                if (weapon.Equals("FLAK") || weapon.Equals("DEVASTATOR")) return 5;
                if (weapon.Equals("THUNDERBOLT") || weapon.Equals("TIMEBOMB")) return 6;
                if (weapon.Equals("LANCER") || weapon.Equals("VORTEX")) return 7;
                else
                {
                    uConsole.Log("-AUTOORDERSELECT- [ERROR] getWeaponIconIndex didnt recognise the given weapon string");
                    return 0;
                }
            }

            public static bool[] isPrimarySelected = new bool[8];
            public static bool[] isSecondarySelected = new bool[8];

            private static Color UIColorPrimaries;
            private static Color UIColorSecondaries;
        }
        
       
    }
}
