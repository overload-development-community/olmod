using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Overload;
using UnityEngine;

namespace GameMod
{
    class MPAutoSelectionUI
    {
        // Menu manager
        [HarmonyPatch(typeof(MenuManager), "MpCustomizeUpdate")]
        class MpCustomizeMenuLogic
        {
            static IEnumerable<CodeInstruction> Transpiler(ILGenerator ilGen, IEnumerable<CodeInstruction> codes)
            {
                var twoCount = 0;
                var threeCount = 0;
                foreach (var code in codes)
                {
                    // Allow all 4 menu options to be scrolled through.
                    if (code.opcode == OpCodes.Ldc_I4_2)
                    {
                        twoCount++;
                        if (twoCount == 1)
                        {
                            code.opcode = OpCodes.Ldc_I4_3;
                        }
                    }
                    else if (code.opcode == OpCodes.Ldc_I4_3)
                    {
                        threeCount++;
                        if (threeCount == 2 || threeCount == 3)
                        {
                            code.opcode = OpCodes.Ldc_I4_4;
                        }
                    }

                    // Prevent profile corruption when selecting autoselect options.  Adds a "case 203" to several switch statements in the function.
                    if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(UIManager), "m_menu_selection"))
                    {
                        if (code.labels.Count == 3)
                        {
                            Label l = ilGen.DefineLabel();
                            code.labels.Add(l);
                        }
                    }
                    yield return code;
                }
            }

            public static int selected;
            public static int selected2;
            public static int loadout1LastTick;
            public static int loadout2LastTick;

            public static void Postfix()
            {
                selected = DrawMpAutoselectOrderingScreen.returnPrimarySelected();
                selected2 = DrawMpAutoselectOrderingScreen.returnSecondarySelected();
                switch (MenuManager.m_menu_sub_state)
                {
                    case MenuSubState.ACTIVE:
                        if (MenuManager.m_menu_micro_state == 3)
                        {
                            switch (UIManager.m_menu_selection)
                            {
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
                                        DrawMpAutoselectOrderingScreen.saveToFile();
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
                                        DrawMpAutoselectOrderingScreen.saveToFile();
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
                                        DrawMpAutoselectOrderingScreen.saveToFile();
                                    }
                                    break;
                                case 2104: //
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
                                        DrawMpAutoselectOrderingScreen.saveToFile();
                                    }
                                    break;




                                default:
                                    if (UIManager.PushedSelect(100) && UIManager.m_menu_selection == 100)
                                    {
                                        uConsole.Log("Definitly 203 " + Player.Mp_loadout1 + " : " + Player.Mp_loadout2);
                                        UIManager.DestroyAll(false);
                                        MenuManager.PlaySelectSound(1f);
                                        if (MPAutoSelection.isCurrentlyInLobby)
                                        {
                                            MenuManager.ChangeMenuState(MenuState.MP_PRE_MATCH_MENU, false);

                                        }
                                        else
                                        {
                                            MenuManager.ChangeMenuState(MenuState.MP_MENU, false);
                                        }
                                        DrawMpAutoselectOrderingScreen.isInitialised = false;

                                    }
                                    break;

                            }
                        }
                        else
                        {
                            //uConsole.Log("NOT 203 "+Player.Mp_loadout1 + " : " + Player.Mp_loadout2);
                            if (Player.Mp_loadout1 == 203 || Player.Mp_loadout2 == 203)
                            {
                                Player.Mp_loadout1 = loadout1LastTick;
                                Player.Mp_loadout2 = loadout2LastTick;
                            }
                            else
                            {
                                loadout1LastTick = Player.Mp_loadout1;
                                loadout2LastTick = Player.Mp_loadout2;
                            }
                            if (UIManager.PushedSelect(100) && UIManager.m_menu_selection == 203)
                            {
                                //MenuManager.SetDefaultSelection(-1);
                                MenuManager.m_menu_micro_state = 3;
                                MenuManager.UIPulse(1f);
                                GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                            }

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
                DrawMpAutoselectOrderingScreen.saveToFile();
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
                DrawMpAutoselectOrderingScreen.saveToFile();
            }

            private static void doSelectedStuffForPrimary(int i)
            {
                if (selected < 1)
                {
                    DrawMpAutoselectOrderingScreen.isPrimarySelected[i] = true;
                    GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                }
                else
                {
                    if (DrawMpAutoselectOrderingScreen.isPrimarySelected[i])
                    {
                        DrawMpAutoselectOrderingScreen.isPrimarySelected[i] = false;
                        GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                    }
                    else
                    {
                        DrawMpAutoselectOrderingScreen.isPrimarySelected[i] = true;
                        DrawMpAutoselectOrderingScreen.SwapSelectedPrimary();
                        SFXCueManager.PlayCue2D(SFXCue.guidebot_objective_found, 0.8f, 0f, 0f, false);
                    }
                }
            }


            private static void doSelectedStuffForSecondary(int i)
            {
                if (selected2 < 1)
                {
                    DrawMpAutoselectOrderingScreen.isSecondarySelected[i] = true;
                    GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                }
                else
                {
                    if (DrawMpAutoselectOrderingScreen.isSecondarySelected[i])
                    {
                        DrawMpAutoselectOrderingScreen.isSecondarySelected[i] = false;
                        GameManager.m_audio.PlayCue2D(364, 0.4f, 0.07f, 0f, false);
                    }
                    else
                    {
                        DrawMpAutoselectOrderingScreen.isSecondarySelected[i] = true;
                        DrawMpAutoselectOrderingScreen.SwapSelectedSecondary();
                        SFXCueManager.PlayCue2D(SFXCue.guidebot_objective_found, 0.8f, 0f, 0f, false);
                    }
                }
            }
        }




        // Adds the Auto order entry in the customize menu
        [HarmonyPatch(typeof(UIElement), "DrawMpTabs")]
        internal class AddFourthTab
        {
            public static bool Prefix(Vector2 pos, int tab_selected, UIElement __instance)
            {
                float w = 378f; // 511
                __instance.DrawWideBox(pos, w, 22f, UIManager.m_col_ub2, __instance.m_alpha, 7);
                string[] array = new string[]
                {
                __instance.GetMpTabName(0),
                __instance.GetMpTabName(1),
                __instance.GetMpTabName(2),
                "AUTOSELECT"
                };

                for (int i = 0; i < array.Length; i++)
                {
                    pos.x = (((float)i - 1f) * 198f) - 99f;//265 -132
                    __instance.TestMouseInRect(pos, 84f, 16f, 200 + i, false); // original value = 112
                    if (UIManager.m_menu_selection == 200 + i)
                    {
                        __instance.DrawWideBox(pos, 84f, 19f, UIManager.m_col_ui4, __instance.m_alpha, 7);
                    }
                    if (i == tab_selected)
                    {
                        __instance.DrawWideBox(pos, 84f, 16f, UIManager.m_col_ui4, __instance.m_alpha, 12);
                        __instance.DrawStringSmall(array[i], pos, 0.6f, StringOffset.CENTER, UIManager.m_col_ub3, 1f, -1f);
                    }
                    else
                    {
                        __instance.DrawWideBox(pos, 84f, 16f, UIManager.m_col_ui0, __instance.m_alpha, 8);
                        __instance.DrawStringSmall(array[i], pos, 0.6f, StringOffset.CENTER, UIManager.m_col_ui1, 1f, -1f);
                    }
                }
                return false;
            }
        }






        [HarmonyPatch(typeof(UIElement), "DrawMpCustomize")]
        internal class DrawMpAutoselectOrderingScreen
        {
            static string[] PrimaryPriorityArray = new string[8];
            static string[] SecondaryPriorityArray = new string[8];

            static void Postfix(UIElement __instance)
            {
                //Initialise();
                if (isInitialised == false)
                {
                    Initialise();
                    isInitialised = true; //should be set to false when leaving the MpCustomize Menu
                }

                int menu_micro_state = MenuManager.m_menu_micro_state;
                if (menu_micro_state == 3)
                {
                    // Draw the Autoselect Ordering menu
                    DrawPriorityList(__instance);
                }
            }

            public static void Initialise()
            {
                Primary[0] = MPAutoSelection.PrimaryPriorityArray[0];
                Primary[1] = MPAutoSelection.PrimaryPriorityArray[1];
                Primary[2] = MPAutoSelection.PrimaryPriorityArray[2];
                Primary[3] = MPAutoSelection.PrimaryPriorityArray[3];
                Primary[4] = MPAutoSelection.PrimaryPriorityArray[4];
                Primary[5] = MPAutoSelection.PrimaryPriorityArray[5];
                Primary[6] = MPAutoSelection.PrimaryPriorityArray[6];
                Primary[7] = MPAutoSelection.PrimaryPriorityArray[7];

                Secondary[0] = MPAutoSelection.SecondaryPriorityArray[0];
                Secondary[1] = MPAutoSelection.SecondaryPriorityArray[1];
                Secondary[2] = MPAutoSelection.SecondaryPriorityArray[2];
                Secondary[3] = MPAutoSelection.SecondaryPriorityArray[3];
                Secondary[4] = MPAutoSelection.SecondaryPriorityArray[4];
                Secondary[5] = MPAutoSelection.SecondaryPriorityArray[5];
                Secondary[6] = MPAutoSelection.SecondaryPriorityArray[6];
                Secondary[7] = MPAutoSelection.SecondaryPriorityArray[7];
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
                string temp = Primary[selection[0]];
                Primary[selection[0]] = Primary[selection[1]];
                Primary[selection[1]] = temp;

                isPrimarySelected[selection[0]] = false;
                isPrimarySelected[selection[1]] = false;
                MPAutoSelection.PrimaryNeverSelect[selection[0]] = false;
                MPAutoSelection.PrimaryNeverSelect[selection[1]] = false;

                saveToFile();
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
                string temp = Secondary[selection[0]];
                Secondary[selection[0]] = Secondary[selection[1]];
                Secondary[selection[1]] = temp;

                isSecondarySelected[selection[0]] = false;
                isSecondarySelected[selection[1]] = false;
                MPAutoSelection.SecondaryNeverSelect[selection[0]] = false;
                MPAutoSelection.SecondaryNeverSelect[selection[1]] = false;

                saveToFile();
                MPAutoSelection.Initialise();
            }

            public static void saveToFile()
            {
                using (StreamWriter sw = File.CreateText(MPAutoSelection.textFile))
                {
                    sw.WriteLine(Primary[0]);
                    sw.WriteLine(Primary[1]);
                    sw.WriteLine(Primary[2]);
                    sw.WriteLine(Primary[3]);
                    sw.WriteLine(Primary[4]);
                    sw.WriteLine(Primary[5]);
                    sw.WriteLine(Primary[6]);
                    sw.WriteLine(Primary[7]);
                    sw.WriteLine(Secondary[0]);
                    sw.WriteLine(Secondary[1]);
                    sw.WriteLine(Secondary[2]);
                    sw.WriteLine(Secondary[3]);
                    sw.WriteLine(Secondary[4]);
                    sw.WriteLine(Secondary[5]);
                    sw.WriteLine(Secondary[6]);
                    sw.WriteLine(Secondary[7]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[0]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[1]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[2]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[3]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[4]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[5]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[6]);
                    sw.WriteLine(MPAutoSelection.PrimaryNeverSelect[7]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[0]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[1]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[2]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[3]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[4]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[5]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[6]);
                    sw.WriteLine(MPAutoSelection.SecondaryNeverSelect[7]);
                    sw.WriteLine(MPAutoSelection.primarySwapFlag);
                    sw.WriteLine(MPAutoSelection.secondarySwapFlag);
                    sw.WriteLine("False");//MPAutoSelection.COswapToHighest);
                    sw.WriteLine("False");//MPAutoSelection.patchPrevNext);
                    sw.WriteLine(MPAutoSelection.zorc);
                    sw.WriteLine(MPAutoSelection.miasmic);
                }
            }

            public static string selectionToDescription(int n)
            {
                if (n == 2100) return "TOGGLES WETHER THE WHOLE FMOD SHOULD BE ACTIVE";
                if (n == 2101) return "REPLACES THE `PREV/NEXT WEAPON` FUNCTION WITH `SWAP TO NEXT HIGHER/LOWER PRIORITIZED WEAPONS`";
                if (n <= 2017 && n >= 2000) return "TOGGLES WETHER THIS WEAPON SHOULD NEVER BE SELECTED";
                if (n <= 1735 && n >= 1720) return "CHANGE THE ORDER BY CLICKING AT THE TWO WEAPONS YOU WANT TO SWAP";
                if (n == 2103) return "TOGGLES EVERYTHING RELATED TO PRIMARY WEAPONS IN THIS MOD";
                if (n == 2102) return "TOGGLES EVERYTHING RELATED TO SECONDARY WEAPONS IN THIS MOD";
                if (n == 2104) return "DISPLAY A WARNING IF A DEVASTATOR GETS AUTOSELECTED";
                if (n == 2105) return "SETS WETHER ON PICKUP SHOULD SWAP TO THE PICKED UP (IF VALID) OR THE HIGHEST";
                else
                {
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


            public static void DrawPriorityList(UIElement uie)
            {
                UIManager.X_SCALE = 0.2f;
                UIManager.ui_bg_dark = true;
                uie.DrawMenuBG();

                Vector2 position = Vector2.up * (UIManager.UI_TOP + 70f);
                position.y += 164f;
                Vector2 position2 = position;
                position.x -= 160f;
                position2.x += 160f;

                UIColorPrimaries = MPAutoSelection.primarySwapFlag ? UIManager.m_col_ui4 : UIManager.m_col_ui0;
                UIColorSecondaries = MPAutoSelection.secondarySwapFlag ? UIManager.m_col_ui4 : UIManager.m_col_ub0;

                Vector2 left = position;
                Vector2 right = position;
                left.x += 75;
                right.x += 240;

                //Draw the neverselect Buttons
                for (int i = 0; i < 8; i++)
                {
                    int primaryindex = getWeaponIconIndex(MPAutoSelection.PrimaryPriorityArray[i]);
                    int secondaryindex = getWeaponIconIndex(MPAutoSelection.SecondaryPriorityArray[i]);
                    UIManager.DrawSpriteUI(left, 0.15f, 0.15f, UIColorPrimaries, uie.m_alpha, 26 + primaryindex);
                    UIManager.DrawSpriteUI(right, 0.15f, 0.15f, UIColorSecondaries, uie.m_alpha, 104 + secondaryindex);

                    left.y += 50;
                    right.y += 50;
                    if (DrawMpAutoselectOrderingScreen.isPrimarySelected[i])
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
                    uie.SelectAndDrawHalfItem(Primary[i], position, 1720 + i, false);
                    position.y += 50;


                    if (DrawMpAutoselectOrderingScreen.isSecondarySelected[i])
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
                    uie.SelectAndDrawHalfItem(Secondary[i], position2, 1728 + i, false);
                    position2.y += 50;
                }


                // other Buttons
                position = Vector2.up * (UIManager.UI_TOP + 70f);
                position.y += 164f;
                position.x += 540f;
                uie.SelectAndDrawItem("Status: " + ((MPAutoSelection.primarySwapFlag || MPAutoSelection.secondarySwapFlag) ? "ACTIVE" : "INACTIVE"), position, 2100, false, 0.3f, 0.45f);
                position.y += 52f;
                position.x += 5f;
                uie.SelectAndDrawItem("Weapon Logic: " + (MPAutoSelection.primarySwapFlag ? "ON" : "OFF"), position, 2103, false, 0.27f, 0.4f);
                position.y += 50f;
                uie.SelectAndDrawItem("Missile Logic: " + (MPAutoSelection.secondarySwapFlag ? "ON" : "OFF"), position, 2102, false, 0.27f, 0.4f);


                position.x -= 5f;
                position.y += 247;
                uie.SelectAndDrawItem("ALERT: " + (MPAutoSelection.zorc ? "ON" : "OFF"), position, 2104, false, 0.3f, 0.45f);



                // Button description 
                position2.x -= 160f;
                position2.y -= 14f;
                string k = selectionToDescription(UIManager.m_menu_selection);
                MPAutoSelection.last_valid_description = k;
                uie.DrawLabelSmall(position2, k, 500f);

                typeof(UIElement).GetMethod("DrawWrappedText", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                    .Invoke(uie, new object[] { "To enable autoselect, set the option at \"Options\", \"Control Options\", \"Advanced Options\", \"Primary Auto-Select\" to \"Never\".", new Vector2(UIManager.UI_LEFT + 35f, UIManager.UI_TOP + 234f), 0.4f, 15f, 220f, StringOffset.LEFT, float.MaxValue, 0f, 0f });
            }
            public static bool isInitialised = false;

            public static string[] Primary = {
                MPAutoSelection.PrimaryPriorityArray[0],
                MPAutoSelection.PrimaryPriorityArray[1],
                MPAutoSelection.PrimaryPriorityArray[2],
                MPAutoSelection.PrimaryPriorityArray[3],
                MPAutoSelection.PrimaryPriorityArray[4],
                MPAutoSelection.PrimaryPriorityArray[5],
                MPAutoSelection.PrimaryPriorityArray[6],
                MPAutoSelection.PrimaryPriorityArray[7],
            };
            public static string[] Secondary = {
                MPAutoSelection.SecondaryPriorityArray[0],
                MPAutoSelection.SecondaryPriorityArray[1],
                MPAutoSelection.SecondaryPriorityArray[2],
                MPAutoSelection.SecondaryPriorityArray[3],
                MPAutoSelection.SecondaryPriorityArray[4],
                MPAutoSelection.SecondaryPriorityArray[5],
                MPAutoSelection.SecondaryPriorityArray[6],
                MPAutoSelection.SecondaryPriorityArray[7]
            };
            public static bool[] isPrimarySelected = new bool[8];
            public static bool[] isSecondarySelected = new bool[8];

            private static Color UIColorPrimaries;
            private static Color UIColorSecondaries;
        }
    }
}
