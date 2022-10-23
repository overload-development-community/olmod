using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public static class Menus
    {

        //public static MenuState msServerBrowser = (MenuState)75;
        //public static MenuState msLagCompensation = (MenuState)76;
        public static MenuState msAutoSelect = (MenuState)77;
        public static MenuState msAxisCurveEditor = (MenuState)78;
        //public static MenuState msTeamColors = (MenuState)79;
        public static MenuState msChangeTeam = (MenuState)80;
        //public static UIElementType uiServerBrowser = (UIElementType)89;
        //public static UIElementType uiLagCompensation = (UIElementType)90;
        public static UIElementType uiAutoSelect = (UIElementType)91;
        public static UIElementType uiAxisCurveEditor = (UIElementType)92;
        //public static UIElementType uiTeamColors = (UIElementType)93;
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
        public static bool mms_always_cloaked { get; set; }
        public static bool mms_allow_smash { get; set; }
        public static bool mms_damage_numbers { get; set; }
        public static bool mms_assist_scoring { get; set; } = true;
        public static bool mms_team_color_default { get; set; } = true;
        public static int mms_team_color_self = 5;
        public static int mms_team_color_enemy = 6;

        public static bool mms_team_health = true;
        public static MpTeam? mms_team_selection { get; set; } = null;

        public static string GetMMSRearViewPIP()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(RearView.MPMenuManagerEnabled));
        }

        public static int mms_audio_occlusion_strength { get; set; } = 0;
        public static string GetMMSAudioOcclusionStrength()
        {
            switch (mms_audio_occlusion_strength)
            {
                case 0:
                    return "OFF";
                case 1:
                    return "WEAK";
                case 2:
                    return "MEDIUM";
                case 3:
                    return "STRONG";
                default:
                    return "UNKNOWN";
            }
        }

        public static bool mms_directional_warnings { get; set; } = true;
        public static string GetMMSDirectionalWarnings()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_directional_warnings));
        }

        public static int mms_loadout_hotkeys { get; set; } = 1;
        public static string GetMMSLoadoutHotkeys()
        {
            switch (mms_loadout_hotkeys)
            {
                case 0:
                    return "OFF";
                case 1:
                    return "PRIMARY SELECT KEYS";
                case 2:
                    return "SEPARATE PRIMARIES";
                case 3:
                    return "NUMBER KEYS 1-4";
                default:
                    return "UNKNOWN";
            }
        }

        public static string GetMMSAlwaysCloaked()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_always_cloaked));
        }

        public static string GetMMSAllowSmash()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_allow_smash));
        }

        public static string GetMMSDamageNumbers()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_damage_numbers));
        }

        public static string GetMMSScaleRespawnTime()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_scale_respawn_time));
        }

        public static string GetMMSClassicSpawns()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_classic_spawns));
        }

        public static string GetMMSLagCompensationAdvanced()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_lag_compensation_advanced));
        }

        public static string GetMMSAssistScoring()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_assist_scoring));
        }

        public static string GetMMSLagCompensation()
        {
            switch (mms_lag_compensation)
            {
                case 0:
                default:
                    return "OFF";
                case 1:
                    return "SHIPS ONLY";
                case 2:
                    return "WEAPONS ONLY";
                case 3:
                    return "ON";
            }
        }

        public static string GetMMSLagCompensationStrength()
        {
            switch (mms_lag_compensation_strength)
            {
                case 0:
                default:
                    return "WEAK";
                case 1:
                    return "MEDIUM";
                case 2:
                    return "STRONG";
            }
        }

        public static string GetMMSLagCompensationUseInterpolation()
        {
            switch (mms_lag_compensation_use_interpolation)
            {
                case 0:
                default:
                    return "OFF";
                case 1:
                    return "WEAK";
                case 2:
                    return "MEDIUM";
                case 3:
                    return "STRONG";
            }
        }

        public static string GetMMSTeamColorDefault()
        {
            return mms_team_color_default ? Loc.LS("DEFAULT") : Loc.LS("CUSTOM");
        }

        public static string GetMMSTeamColorSelf()
        {
            return MPTeams.ColorName(mms_team_color_self);
        }

        public static string GetMMSTeamColorEnemy()
        {
            return MPTeams.ColorName(mms_team_color_enemy);
        }

        public static void SetLagCompensationDefaults()
        {
            mms_weapon_lag_compensation_max = 100;
            mms_ship_lag_compensation_max = 100;
            mms_weapon_lag_compensation_scale = 100;
            mms_ship_lag_compensation_scale = 100;
            mms_lag_compensation_ship_added_lag = 0;
            mms_lag_compensation_advanced = false;
            mms_lag_compensation_strength = 2;
            mms_lag_compensation_use_interpolation = 0;
            mms_lag_compensation_collision_limit = 0;
        }

        public static int mms_weapon_lag_compensation_max = 100;
        public static int mms_ship_lag_compensation_max = 100;
        public static int mms_weapon_lag_compensation_scale = 100;
        public static int mms_ship_lag_compensation_scale = 100;
        public static int mms_lag_compensation_ship_added_lag = 0;
        public static bool mms_lag_compensation_advanced = false;
        public static int mms_lag_compensation = 3;
        public static int mms_lag_compensation_strength = 2;
        public static int mms_lag_compensation_use_interpolation = 0;
        public static int mms_lag_compensation_collision_limit = 0;
        public static string mms_mp_projdata_fn = "STOCK";
        public static bool mms_sticky_death_summary = false;
        public static int mms_damageeffect_alpha_mult = 30;
        public static int mms_damageeffect_drunk_blur_mult = 10;
        public static int mms_match_time_limit = 60;
        public static bool mms_reduced_ship_explosions = true;
        public static bool mms_show_framerate = false;
        public static int mms_selected_loadout_idx = 0;
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
            position.y += 55f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("ALWAYS CLOAKED"), position, 15, Menus.GetMMSAlwaysCloaked(), Loc.LS("SHIPS ARE ALWAYS CLOAKED"), 1f, false);
            position.y += 55f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("CLASSIC SPAWNS"), position, 13, Menus.GetMMSClassicSpawns(), Loc.LS("SPAWN WITH IMPULSE+ DUALS AND FALCONS"), 1f, false);
            position.y += 55f;

            if (MenuManager.mms_mode == ExtMatchMode.CTF)
            {
                uie.SelectAndDrawStringOptionItem(Loc.LS("CTF CARRIER BOOSTING"), position, 14, Menus.GetMMSCtfCarrierBoost(), Loc.LS("FLAG CARRIER CAN USE BOOST IN CTF"), 1f, false);
            }
            else
            {
                uie.SelectAndDrawStringOptionItem(Loc.LS("ASSISTS"), position, 18, Menus.GetMMSAssistScoring(), Loc.LS("AWARD POINTS FOR ASSISTING WITH KILLS"), 1f, false);
            }

            position.y += 55f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("PROJECTILE DATA"), position, 16, Menus.mms_mp_projdata_fn == "STOCK" ? "STOCK" : System.IO.Path.GetFileName(Menus.mms_mp_projdata_fn), string.Empty, 1f, false);
            position.y += 55f;

            uie.SelectAndDrawStringOptionItem(Loc.LS("ALLOW SMASH ATTACK"), position, 17, Menus.GetMMSAllowSmash(), Loc.LS("ALLOWS PLAYERS TO USE THE SMASH ATTACK"), 1f, false);
            position.y += 55f;

            uie.SelectAndDrawStringOptionItem(Loc.LS("TB PENETRATION"), position, 20, MPThunderboltPassthrough.isAllowed ? "ON" : "OFF", Loc.LS("ALLOWS THUNDERBOLT SHOTS TO PENETRATE SHIPS"), 1f, false);
            position.y += 55f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("DAMAGE NUMBERS"), position, 21, Menus.GetMMSDamageNumbers(), Loc.LS("SHOWS THE DAMAGE YOU DO TO OTHER SHIPS"), 1f, false);
        }

        private static void AdjustAdvancedPositionCenterColumn(ref Vector2 position)
        {
            position.x -= 600f;
            position.y = col_bot;
        }

        private static void DrawTeamSettings(UIElement uie, ref Vector2 position)
        {
            position.y += 12f;
            uie.SelectAndDrawItem(Loc.LS("TEAM SETTINGS"), position, 19, false, 1f, 0.75f);
            position.y += 62f;
        }

        private static void ResetCenter(UIElement uie, ref Vector2 position)
        {
            position.x += 300f;
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
                    // Add Team Settings
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawMpMatchSetup), "DrawTeamSettings"));
                }

                // Reset center
                if (state == 2 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "SelectAndDrawItem"))
                {
                    state = 3;
                    yield return code;
                    continue;
                }
                if (state == 3 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "SelectAndDrawItem"))
                {
                    state = 4;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawMpMatchSetup), "ResetCenter"));
                    continue;
                }

                yield return code;
            }
        }

        private static void Postfix(UIElement __instance)
        {
            switch (MenuManager.m_menu_micro_state)
            {
                case 10:
                    Player local_player = GameManager.m_local_player;
                    Vector2 position = __instance.m_position;
                    __instance.DrawLabelSmall(Vector2.up * (UIManager.UI_TOP + 70f), Loc.LS("TEAM SETTINGS"), 250f, 24f, 1f);
                    position.y -= 280f;

                    __instance.DrawMenuSeparator(position);
                    position.y += 40f;

                    __instance.SelectAndDrawStringOptionItem(Loc.LS("FRIENDLY FIRE"), position, 3, MenuManager.GetMMSFriendlyFire(), string.Empty, 1.5f, MenuManager.mms_mode == MatchMode.ANARCHY);
                    position.y += 62f;

                    __instance.SelectAndDrawStringOptionItem("TEAM COUNT", position, 8, MPTeams.MenuManagerTeamCount.ToString(), string.Empty, 1.5f,
                        MenuManager.mms_mode == MatchMode.ANARCHY || !MenuManager.m_mp_lan_match);
                    position.y += 62f;

                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SCALE RESPAWN TO TEAM SIZE"), position, 12, Menus.GetMMSScaleRespawnTime(), Loc.LS("AUTOMATICALLY SCALE RESPAWN TIME TO TEAM SIZE (e.g. 4 = 4 seconds)"), 1.5f, !(MenuManager.mms_mode == ExtMatchMode.TEAM_ANARCHY || MenuManager.mms_mode == ExtMatchMode.CTF || MenuManager.mms_mode == ExtMatchMode.MONSTERBALL));
                    position.y += 62f;

                    position.x = -310f;
                    position.y = -90f;
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Add Rear View, Ship Velocity, Frame Rate options to Options -> Cockpit & HUD Options
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawHUDMenu")]
    class Menus_UIElement_DrawHUDMenu
    {
        static bool Prefix(UIElement __instance)
        {
            UIManager.ui_bg_dark = true;
            Vector2 position = __instance.m_position;
            __instance.DrawMenuBG();
            UIElement.ToolTipActive = false;
            __instance.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 20f), Loc.LS("COCKPIT & HUD OPTIONS"), 265f);
            position.y -= 296f;
            __instance.DrawMenuSeparator(position);
            position.y += 40f;

            switch (MenuManager.m_menu_micro_state)
            {
                case 0:
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("UI PRIMARY COLOR"), position, 0, MenuManager.GetHUDColor(), string.Empty, 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("CAMERA SHAKE"), position, 9, MenuManager.GetHUDShake(), string.Empty, 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SCREEN FLICKER"), position, 10, MenuManager.GetHUDFlicker(), string.Empty, 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("RETICLE STYLE"), position, 1, MenuManager.GetHUDReticle(), string.Empty, 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("HUD RIGHT SIDE DISPLAY"), position, 2, MenuManager.GetHUDWeapons(), Loc.LS("SWAP WEAPONS TO THE RIGHT SIDE (BEST FOR GAMEPAD CONTROLS)"), 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHOW LARGE BANNER MESSAGES"), position, 6, MenuManager.GetHUDBannerMessages(), Loc.LS("DISPLAY LARGE BANNER MESSAGES IN CENTER OF SCREEN"), 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHOW TEXT FOR AUDIO MESSAGES"), position, 7, MenuManager.GetTextForAudio(), Loc.LS("DISPLAYS ENGLISH TEXT FOR COMM AND LOG AUDIO MESSAGES"), 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("COCKPIT SWAY"), position, 3, MenuManager.GetHUDSway(), Loc.LS("ADD MOTION TO COCKPIT WHEN MOVING"), 1.5f, false);
                    
                    break;
                case 1:
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHOW SPEEDRUN TIMERS"), position, 8, (!MenuManager.opt_speedrun_timers) ? Loc.LS("NO") : Loc.LS("YES"), Loc.LS("SHOW CURRENT LEVEL AND MISSION TIME ON HUD"), 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHOW REAR VIEW CAMERA"), position, 11, RearView.MenuManagerEnabled ? "ON" : "OFF", "SHOW REAR VIEW CAMERA ON HUD", 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHOW SHIP VELOCITY"), position, 12, HUDVelocity.MenuManagerEnabled ? "ON" : "OFF", "SHOW SHIP VELOCITY ON HUD", 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHOW FRAME RATE"), position, 13, Menus.mms_show_framerate ? "ON" : "OFF", "SHOW FRAME RATE ON HUD (FPS)", 1.5f, false);

                    break;
                default:
                    break;
            }

            position.y = UIManager.UI_BOTTOM - 180f;
            __instance.DrawMenuSeparator(position + Vector2.up * 40f);
            __instance.DrawMenuToolTip(position + Vector2.up * 40f, 15f);
            __instance.DrawPageControls(position + Vector2.up * 85f, string.Format(Loc.LS("PAGE {0} OF {1}"), MenuManager.m_menu_micro_state + 1, 2), true, true, false, false, 420, 302, false);
            position.y = UIManager.UI_BOTTOM - 30f;
            __instance.SelectAndDrawItem(Loc.LS("BACK"), position, 100, false, 1f, 0.75f);
            __instance.MaybeShowMpStatus();

            return false;
        }
    }


    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class Menus_MenuManager_MpMatchSetup
    {
        // Handle match time limit
        static void ProcessMatchTimeLimit()
        {
            Menus.mms_match_time_limit = ((Menus.mms_match_time_limit / 60 + 21 + UIManager.m_select_dir) % 21) * 60;
            MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
        }

        // Handle additional options, Team Settings menu etc
        static void ProcessAdditional()
        {
            if (MenuManager.m_menu_micro_state == 3)
            {
                switch (UIManager.m_menu_selection)
                {
                    case 11:
                        RearView.MPMenuManagerEnabled = !RearView.MPMenuManagerEnabled;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 13:
                        Menus.mms_classic_spawns = !Menus.mms_classic_spawns;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 14:
                        Menus.mms_ctf_boost = !Menus.mms_ctf_boost;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 15:
                        Menus.mms_always_cloaked = !Menus.mms_always_cloaked;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 16:
                        if (UIManager.PushedSelect(100))
                        {
                            var files = new string[] { "STOCK" }.AddRangeToArray(Directory.GetFiles(Config.OLModDir, "projdata-*.txt"));
                            for (int i = 0; i < files.Length; i++)
                            {
                                uConsole.Log(files[i] + ": " + files.Length.ToString());
                                if (Menus.mms_mp_projdata_fn == files[i])
                                {
                                    if (i + 1 < files.Length)
                                    {
                                        Menus.mms_mp_projdata_fn = files[i + 1];
                                    }
                                    else
                                    {
                                        Menus.mms_mp_projdata_fn = files[0];
                                    }
                                    break;
                                }
                            }
                            MenuManager.PlayCycleSound(1f, 1f);
                        }
                        break;
                    case 17:
                        Menus.mms_allow_smash = !Menus.mms_allow_smash;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;

                    case 18:
                        Menus.mms_assist_scoring = !Menus.mms_assist_scoring;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 19:
                        MenuManager.m_menu_micro_state = 10;
                        MenuManager.UIPulse(2f);
                        MenuManager.PlaySelectSound(1f);
                        break;
                    case 20:
                        MPThunderboltPassthrough.isAllowed = !MPThunderboltPassthrough.isAllowed;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 21:
                        Menus.mms_damage_numbers = !Menus.mms_damage_numbers;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;

                }
            }
            else if (MenuManager.m_menu_micro_state == 10)
            {
                // Team Settings window
                switch (UIManager.m_menu_selection)
                {
                    case 3:
                        MenuManager.mms_friendly_fire = 1 - MenuManager.mms_friendly_fire;
                        MenuManager.PlayCycleSound(1f, 1f);
                        break;
                    case 8:
                        MPTeams.MenuManagerTeamCount = MPTeams.Min +
                            (MPTeams.MenuManagerTeamCount - MPTeams.Min + (MPTeams.Max - MPTeams.Min + 1) + UIManager.m_select_dir) %
                            (MPTeams.Max - MPTeams.Min + 1);
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 12:
                        Menus.mms_scale_respawn_time = !Menus.mms_scale_respawn_time;
                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                        break;
                    case 100:
                        MenuManager.m_menu_micro_state = 3;
                        MenuManager.UIPulse(2f);
                        MenuManager.PlaySelectSound(1f);
                        return;
                    default:
                        return;
                }
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            List<Label> labels = new List<Label>();
            foreach (var code in codes)
            {
                if (state == 0 && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(MenuManager), "mms_time_limit"))
                {
                    state = 1;
                    labels = code.labels;
                }

                if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(MenuManager), "PlayCycleSound"))
                {
                    code.operand = AccessTools.Method(typeof(Menus_MenuManager_MpMatchSetup), "ProcessMatchTimeLimit");
                    code.labels = labels;
                    state = 2;
                }

                if (state == 1)
                    continue;

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(MenuManager), "MaybeReverseOption"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_MpMatchSetup), "ProcessAdditional"));
                    continue;
                }

                // Remove processing of mms_friendly_fire, moved to ProcessAdditional()
                if (code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(MenuManager), "mms_friendly_fire"))
                {
                    yield return new CodeInstruction(OpCodes.Pop);
                    continue;
                }


                yield return code;
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
        static bool Prefix(UIElement __instance)
        {
            UIManager.X_SCALE = 0.2f;
            UIManager.ui_bg_dark = true;
            __instance.DrawMenuBG();
            UIElement.ToolTipActive = false;
            Vector2 position = __instance.m_position;
            position.y = UIManager.UI_TOP + 60f;
            __instance.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 20f), Loc.LS("MULTIPLAYER OPTIONS"), 265f);
            position.y += 20f;
            //__instance.DrawMenuSeparator(position);
            DrawTabs(__instance, position, MenuManager.m_menu_micro_state);
            position.y += 40f;
            __instance.DrawMenuSeparator(position);
            position.y += 40f;

            switch (MenuManager.m_menu_micro_state)
            {
                case 0:
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("TEXT CHAT"), position, 0, MenuManager.GetMPTextChat(), string.Empty, 1.5f, false);
                    position.y += 52f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("AUTO-RESPAWN TIMER"), position, 2, MenuManager.GetToggleSetting(MenuManager.opt_mp_auto_respawn), string.Empty, 1.5f, false);
                    position.y += 52f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("STICKY DEATH SUMMARY"), position, 3, Menus.mms_sticky_death_summary ? "YES" : "NO", "KEEP DEATH SUMMARY ON THE SCREEN AFTER LETTING GO OF THE TOGGLE");
                    position.y += 52f;
                    __instance.SelectAndDrawSliderItem(Loc.LS("DAMAGE BLUR INTENSITY"), position, 4, ((float)Menus.mms_damageeffect_drunk_blur_mult) / 100f);
                    position.y += 52f;
                    __instance.SelectAndDrawSliderItem(Loc.LS("DAMAGE COLOR INTENSITY"), position, 5, ((float)Menus.mms_damageeffect_alpha_mult) / 100f);
                    position.y += 52f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHIP EXPLOSION EFFECTS"), position, 6, Menus.mms_reduced_ship_explosions ? Loc.LS("REDUCED") : Loc.LS("FULL"), Loc.LS("REDUCED VISUAL CLUTTER DURING DEATH ROLL"));
                    position.y += 52f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("INDIVIDUAL PLAYER COLORS"), position, 7, MPColoredPlayerNames.isActive ? "ON" : "OFF", Loc.LS("MAKES NAMES MORE RECOGNIZABLE AND DISTINCT BY MAKING THEM BIGGER AND COLORING THEM BY PLAYER [ANARCHY ONLY]"));
                    position.y += 52f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("PROFANITY FILTER"), position, 8, DisableProfanityFilter.profanity_filter ? "ON" : "OFF", Loc.LS(""));
                    position.y += 52f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("LOADOUT SELECTION HOTKEYS"), position, 9, Menus.GetMMSLoadoutHotkeys(), Loc.LS("WEAPON SELECTION HOTKEYS WILL QUICK-SWAP BETWEEN LOADOUTS"));
                    position.y += 68f;
                    __instance.SelectAndDrawItem(Loc.LS("QUICK CHAT"), position, 1, false, 1f, 0.75f);
                    break;
                case 1:
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("TEAMMATE NAMES"), position, 0, MenuManager.GetMPTeammateNames(), string.Empty, 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("TEAM COLORS"), position, 1, Menus.GetMMSTeamColorDefault(), "DISPLAY TEAM COLORS IN DEFAULT ORANGE/BLUE OR CUSTOM", 1.5f, false);
                    position.y += 64f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("MY TEAM"), position, 2, Menus.GetMMSTeamColorSelf(), "", 1.5f, Menus.mms_team_color_default);
                    position.y += 64f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("ENEMY TEAM"), position, 3, Menus.GetMMSTeamColorEnemy(), "", 1.5f, Menus.mms_team_color_default);
                    position.y += 64f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("SHOW TEAM HEALTH"), position, 4, Menus.mms_team_health ? "ON" : "OFF", "SETS WHETHER THE HEALTH OF TEAMMATES SHOULD GET DISPLAYED", 1.5f, false);
                    break;
                case 2:
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("LAG COMPENSATION"), position, 1, Menus.GetMMSLagCompensation(), "ENABLE LAG COMPENSATION FOR MULTIPLAYER GAMES", 1.5f, false);
                    position.y += 62f;
                    __instance.SelectAndDrawStringOptionItem(Loc.LS("USE ADVANCED SETTINGS"), position, 2, Menus.GetMMSLagCompensationAdvanced(), "SHOW ADVANCED SETTINGS TO FURTHER FINE TUNE YOUR LAG COMPENSATION", 1.5f, false);
                    position.y += 62f;
                    //__instance.DrawTextLineSmall("WARNING: ONCE ON, TURNING OFF ADVANCED SETTINGS", position);
                    //position.y += 28f;
                    //__instance.DrawTextLineSmall("WILL RESET YOUR LAG COMPENSATION SETTINGS TO DEFAULT.", position);
                    //position.y += 62f;
                    if (!Menus.mms_lag_compensation_advanced)
                    {
                        SelectAndDrawSliderItem(__instance, Loc.LS("MAX PING TO COMPENSATE"), position, 3, Menus.mms_ship_lag_compensation_max, 250, "LIMIT LAG COMPENSATION IF YOUR PING EXCEEDS THIS AMOUNT. HAS NO EFFECT WHEN YOUR PING IS LOWER THAN THIS AMOUNT", Menus.mms_lag_compensation == 0);
                        position.y += 62f;
                        __instance.SelectAndDrawStringOptionItem(Loc.LS("LAG COMPENSATION STRENGTH"), position, 4, Menus.GetMMSLagCompensationStrength(), "SCALES THE STRENGTH OF LAG COMPENSATION RELATIVE TO YOUR PING", fade: Menus.mms_lag_compensation == 0);
                        position.y += 62f;
                        __instance.SelectAndDrawStringOptionItem(Loc.LS("USE INTERPOLATION"), position, 5, Menus.GetMMSLagCompensationUseInterpolation(), "HOW STRONGLY TO INTERPOLATE SHIP POSITIONS AT THE COST OF ADDED LAG." + Environment.NewLine + "A STRONGER VALUE WILL BETTER SHOW SHIP POSITIONS WITHOUT GUESSING, BUT REQUIRE YOU TO LEAD SHIPS MORE", fade: Menus.mms_lag_compensation == 1 || Menus.mms_lag_compensation == 3);
                    }
                    else
                    {
                        SelectAndDrawSliderItem(__instance, Loc.LS("MAX PING TO COMPENSATE FOR WEAPONS"), position, 6, Menus.mms_weapon_lag_compensation_max, 250, "LIMIT WEAPON LAG COMPENSATION IF YOUR PING EXCEEDS THIS AMOUNT. HAS NO EFFECT WHEN YOUR PING IS LOWER THAN THIS AMOUNT." + Environment.NewLine + "AT HIGHER PING, A LOWER SETTING LIMITS HOW FAR FROM THE FIRING SHIP WEAPONS WILL BE DRAWN, AT THE COST OF HAVING TO LEAD YOUR DODGES MORE", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 1);
                        position.y += 62f;
                        SelectAndDrawSliderItem(__instance, Loc.LS("MAX PING TO COMPENSATE FOR SHIPS"), position, 7, Menus.mms_ship_lag_compensation_max, 250, "LIMIT SHIP LAG COMPENSATION IF YOUR PING EXCEEDS THIS AMOUNT. HAS NO EFFECT WHEN YOUR PING IS LOWER THAN THIS AMOUNT." + Environment.NewLine + "AT HIGHER PING, A LOWER SETTING LIMITS HOW FAR INTO THE FUTURE SHIP POSITIONS WILL BE GUESSED, AT THE COST OF HAVING TO LEAD YOUR SHOTS MORE", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 2);
                        position.y += 62f;
                        SelectAndDrawSliderItem(__instance, Loc.LS("WEAPON LAG COMPENSATION SCALE"), position, 8, Menus.mms_weapon_lag_compensation_scale, 100, "THE SCALE AT WHICH WEAPON LAG IS COMPENSATED MEASURED AS A PERCENTAGE OF THE AMOUNT OF PING YOU ARE COMPENSATING FOR." + Environment.NewLine + "A SCALE OF 100% WILL MAKE YOUR DODGING CLOSELY MATCH THE SERVER WHEN YOUR PING IS LESS THAN YOUR MAX PING TO COMPENSATE FOR WEAPONS", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 1);
                        position.y += 62f;
                        SelectAndDrawSliderItem(__instance, Loc.LS("SHIP LAG COMPENSATION SCALE"), position, 9, Menus.mms_ship_lag_compensation_scale, 100, "THE SCALE AT WHICH SHIP LAG IS COMPENSATED MEASURED AS A PERCENTAGE OF THE AMOUNT OF PING YOU ARE COMPENSATING FOR." + Environment.NewLine + "A SCALE OF 100% WILL MAKE YOUR SHOTS THAT HIT SHIPS CLOSELY MATCH THE SERVER WHEN YOUR PING IS LESS THAN YOUR MAX PING TO COMPENSATE FOR SHIPS", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 2);
                        position.y += 62f;
                        SelectAndDrawSliderItem(__instance, Loc.LS("SHIP LAG ADDED"), position, 10, Menus.mms_lag_compensation_ship_added_lag, 50, "ADDS A SET AMOUNT OF LAG TO THE END OF THE SHIP LAG COMPENSATION CALCULATIONS. USEFUL WHEN SHIP LAG COMPENSATION IS TURNED OFF." + Environment.NewLine + "A HIGHER SETTING WILL BETTER SHOW SHIP POSITIONS WITHOUT GUESSING, BUT REQUIRE YOU TO LEAD SHIPS MORE");
                        position.y += 62f;
                        SelectAndDrawSliderItem(__instance, Loc.LS("LIMIT SHIPS DIVING INTO WALLS"), position, 11, Menus.mms_lag_compensation_collision_limit, 100, "LIMIT HOW FAR SHIPS MIGHT DIVE INTO WALLS (BUT SHIPS MIGHT APPEAR STUCK AT THE WALLS FOR SHORT MOMENTS INSTEAD)." + Environment.NewLine + "0 FOR UNLIMITED (NO CALCULATION OVERHEAD), OTHERWISE PERCENTAGE OF SHIP DIAMETER WHICH MUST REMAIN VISIBLE (100 = NO DIVE-IN AT ALL).");
                    }
                    break;
                default:
                    break;
            }

            __instance.DrawMenuSeparator(position + Vector2.up * 40f);
            __instance.DrawMenuToolTip(position + Vector2.up * 40f, 15f);
            position.y = UIManager.UI_BOTTOM - 23f;
            __instance.SelectAndDrawItem(Loc.LS("BACK"), position, 100, false, 1f, 0.75f);
            __instance.MaybeShowMpStatus();

            return false;
        }

        public static void DrawTabs(UIElement uie, Vector2 pos, int tab_selected)
        {
            float w = 378f;
            uie.DrawWideBox(pos, w, 22f, UIManager.m_col_ub2, uie.m_alpha, 7);
            string[] array = new string[]
            {
                "GENERAL",
                "TEAM",
                "LAG COMPENSATION"
            };
            for (int i = 0; i < array.Length; i++)
            {
                pos.x = ((float)i - 1f) * 265f;
                uie.TestMouseInRect(pos, 112f, 16f, 200 + i, false);
                if (UIManager.m_menu_selection == 200 + i)
                {
                    uie.DrawWideBox(pos, 112f, 19f, UIManager.m_col_ui4, uie.m_alpha, 7);
                }
                if (i == tab_selected)
                {
                    uie.DrawWideBox(pos, 112f, 16f, UIManager.m_col_ui4, uie.m_alpha, 12);
                    uie.DrawStringSmall(array[i], pos, 0.6f, StringOffset.CENTER, UIManager.m_col_ub3, 1f, -1f);
                }
                else
                {
                    uie.DrawWideBox(pos, 112f, 16f, UIManager.m_col_ui0, uie.m_alpha, 8);
                    uie.DrawStringSmall(array[i], pos, 0.6f, StringOffset.CENTER, UIManager.m_col_ui1, 1f, -1f);
                }
            }
        }

        // Tweak of original SelectAndDrawSliderItem to support non-100 max, tooltip
        public static void SelectAndDrawSliderItem(UIElement uie, string s, Vector2 pos, int selection, float amt, float max, string tool_tip, bool fade = false)
        {
            float num = 750f;
            int quad_index = UIManager.m_quad_index;
            if (!fade)
                uie.TestMouseInRect(pos, num * 0.5f + 22f, 24f, selection, true);
            float x = pos.x;
            pos.x += num * 0.5f - 123f;
            if (!fade)
                uie.TestMouseInRectSlider(pos, 132f, 22f, selection, false);
            pos.x = x;
            bool flag = UIManager.m_menu_selection == selection;
            if (flag)
            {
                MenuManager.option_dir = true;
                if (tool_tip != string.Empty)
                {
                    UIElement.ToolTipActive = true;
                    UIElement.ToolTipTitle = s;
                    UIElement.ToolTipDescription = tool_tip;
                }
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
            if (fade)
                UIManager.PreviousQuadsAlpha(quad_index, 0.3f);
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpOptionsUpdate")]
    class Menus_MenuManager_MpOptionsUpdate
    {
        private static MethodInfo _MenuManager_GoBack_Method = AccessTools.Method(typeof(MenuManager), "GoBack");
        private static MethodInfo _MenuManager_PlayHighlightSound_Method = AccessTools.Method(typeof(MenuManager), "PlayHighlightSound");

        static bool Prefix(ref float ___m_menu_state_timer)
        {
            MenuManager.UpdateMPStatus();
            UIManager.MouseSelectUpdate();
            MenuSubState menu_sub_state = MenuManager.m_menu_sub_state;
            if (menu_sub_state != MenuSubState.INIT)
            {
                if (menu_sub_state == MenuSubState.ACTIVE)
                {
                    UIManager.ControllerMenu();
                    if (Controls.JustPressed(CCInput.MENU_SECONDARY))
                    {
                        MenuManager.m_mp_status_minimized = !MenuManager.m_mp_status_minimized;
                        MenuManager.PlayCycleSound(1f, 1f);
                    }
                    if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()))
                    {
                        MenuManager.MaybeReverseOption();
                        int menu_selection = UIManager.m_menu_selection;
                        switch (menu_selection)
                        {
                            case 200:
                            case 201:
                            case 202:
                                MenuManager.m_menu_micro_state = UIManager.m_menu_selection - 200;
                                MenuManager.UIPulse(1f);
                                _MenuManager_PlayHighlightSound_Method.Invoke(null, new object[] { 0.4f, 0.07f });
                                break;
                            default:
                                if (menu_selection == 100)
                                {
                                    _MenuManager_GoBack_Method.Invoke(null, null);
                                    UIManager.DestroyAll(false);
                                    MenuManager.PlaySelectSound(1f);
                                }
                                break;
                        }

                        switch (MenuManager.m_menu_micro_state)
                        {
                            case 0:
                                switch (menu_selection)
                                {
                                    case 0:
                                        MenuManager.opt_mp_text_chat = (MenuManager.opt_mp_text_chat + 4 + UIManager.m_select_dir) % 4;
                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                        break;
                                    case 1:
                                        MenuManager.ChangeMenuState(MenuState.MP_QC_OPTIONS, false);
                                        UIManager.DestroyAll(false);
                                        MenuManager.PlaySelectSound(1f);
                                        break;
                                    case 2:
                                        MenuManager.opt_mp_auto_respawn = (MenuManager.opt_mp_auto_respawn + 1) % 2;
                                        MenuManager.PlayCycleSound(1f, 1f);
                                        break;
                                    case 3:
                                        Menus.mms_sticky_death_summary = !Menus.mms_sticky_death_summary;
                                        MPDeathReview.stickyDeathReview = Menus.mms_sticky_death_summary;
                                        MenuManager.PlaySelectSound(1f);
                                        break;
                                    case 4:
                                        Menus.mms_damageeffect_drunk_blur_mult = (int)(UIElement.SliderPos * 100f);
                                        if (Input.GetMouseButtonDown(0))
                                            MenuManager.PlayCycleSound(1f, (float)((double)UIElement.SliderPos * 5.0 - 3.0));
                                        break;
                                    case 5:
                                        Menus.mms_damageeffect_alpha_mult = (int)(UIElement.SliderPos * 100f);
                                        if (Input.GetMouseButtonDown(0))
                                            MenuManager.PlayCycleSound(1f, (float)((double)UIElement.SliderPos * 5.0 - 3.0));
                                        break;
                                    case 6:
                                        Menus.mms_reduced_ship_explosions = !Menus.mms_reduced_ship_explosions;
                                        MenuManager.PlaySelectSound(1f);
                                        break;
                                    case 7:
                                        MPColoredPlayerNames.isActive = !MPColoredPlayerNames.isActive;
                                        MenuManager.PlaySelectSound(1f);
                                        break;
                                    case 8:
                                        DisableProfanityFilter.profanity_filter = !DisableProfanityFilter.profanity_filter;
                                        MenuManager.PlaySelectSound(1f);
                                        break;
                                    case 9:
                                        Menus.mms_loadout_hotkeys = (Menus.mms_loadout_hotkeys + UIManager.m_select_dir) % 4;
                                        if (Menus.mms_loadout_hotkeys < 0)
                                        {
                                            Menus.mms_loadout_hotkeys = 3;
                                        }
                                        MenuManager.PlaySelectSound(1f);
                                        break;
                                }
                                break;
                            case 1:
                                switch (menu_selection)
                                {
                                    case 0:
                                        MenuManager.opt_mp_teammates = (MenuManager.opt_mp_teammates + 2 + UIManager.m_select_dir) % 2;
                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                        break;
                                    case 1:
                                        Menus.mms_team_color_default = !Menus.mms_team_color_default;
                                        MenuManager.PlaySelectSound(1f);
                                        MPTeams.UpdateClientColors();
                                        break;
                                    case 2:
                                        Menus.mms_team_color_self = (Menus.mms_team_color_self + 9 + UIManager.m_select_dir) % 9;
                                        if (Menus.mms_team_color_self == Menus.mms_team_color_enemy)
                                            Menus.mms_team_color_self = (Menus.mms_team_color_self + 9 + UIManager.m_select_dir) % 9;
                                        MenuManager.PlaySelectSound(1f);
                                        MPTeams.UpdateClientColors();
                                        break;
                                    case 3:
                                        Menus.mms_team_color_enemy = (Menus.mms_team_color_enemy + 9 + UIManager.m_select_dir) % 9;
                                        if (Menus.mms_team_color_enemy == Menus.mms_team_color_self)
                                            Menus.mms_team_color_enemy = (Menus.mms_team_color_enemy + 9 + UIManager.m_select_dir) % 9;
                                        MenuManager.PlaySelectSound(1f);
                                        MPTeams.UpdateClientColors();
                                        break;
                                    case 4:
                                        Menus.mms_team_health = !Menus.mms_team_health;
                                        MenuManager.PlaySelectSound(1f);
                                        break;
                                }
                                break;
                            case 2:
                                switch (menu_selection)
                                {
                                    case 1:
                                        Menus.mms_lag_compensation = (Menus.mms_lag_compensation + 4 + UIManager.m_select_dir) % 4;
                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                        break;
                                    case 2:
                                        Menus.mms_lag_compensation_advanced = !Menus.mms_lag_compensation_advanced;
                                        if (!Menus.mms_lag_compensation_advanced)
                                        {
                                            Menus.SetLagCompensationDefaults();
                                        }
                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                        break;
                                    case 3:
                                        Menus.mms_ship_lag_compensation_max = (int)(UIElement.SliderPos * 250f);
                                        Menus.mms_weapon_lag_compensation_max = Menus.mms_ship_lag_compensation_max;
                                        break;
                                    case 4:
                                        Menus.mms_lag_compensation_strength = (Menus.mms_lag_compensation_strength + 3 + UIManager.m_select_dir) % 3;
                                        Menus.mms_weapon_lag_compensation_scale = (int)Math.Round((Menus.mms_lag_compensation_strength + 1) * 100f / 3f, 0);
                                        Menus.mms_ship_lag_compensation_scale = Menus.mms_weapon_lag_compensation_scale;
                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                        break;
                                    case 5:
                                        Menus.mms_lag_compensation_use_interpolation = (Menus.mms_lag_compensation_use_interpolation + 4 + UIManager.m_select_dir) % 4;
                                        Menus.mms_lag_compensation_ship_added_lag = (int)Math.Round(Menus.mms_lag_compensation_use_interpolation * 1000f / 60f, 0);
                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                        break;
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
                                        Menus.mms_lag_compensation_ship_added_lag = (int)(UIElement.SliderPos * 50f);
                                        break;
                                    case 11:
                                        Menus.mms_lag_compensation_collision_limit = (int)(UIElement.SliderPos * 100f + 0.5f);
                                        break;
                                }
                                break;
                            default:
                                break;
                        }
                        MenuManager.UnReverseOption();
                    }
                }
            }
            else if (___m_menu_state_timer > 0.25f)
            {
                UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.MP_OPTIONS);
                MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                MenuManager.SetDefaultSelection(0);
            }
            else
            {
                MenuManager.m_menu_micro_state = 0;
            }

            return false;
        }

    }

    /// <summary>
    /// Shadow Settings tooltip does not indicate to user that game restart is required
    /// https://github.com/overload-development-community/olmod/issues/108
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawGraphicsMenu")]
    class Menus_UIElement_DrawGraphicsMenu
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "SHADOW RESOLUTION AND SHADOW DRAW DISTANCE")
                    code.operand += " (GAME RESTART REQUIRED)";
                yield return code;
            }
        }
    }

    /// <summary>
    /// https://github.com/overload-development-community/olmod/issues/148
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawSoundMenu")]
    class Menus_UIElement_DrawSoundMenu
    {
        private static void DrawAdditionalSoundOptions(UIElement uie, ref Vector2 position)
        {
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("AUDIO OCCLUSION STRENGTH"), position, 6, Menus.GetMMSAudioOcclusionStrength(), Loc.LS("SOUND EFFECTS OUT OF LINE-OF-SIGHT WILL BE FILTERED DEPENDING ON DISTANCE"));
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("DIRECTIONAL HOMING WARNINGS"), position, 7, Menus.GetMMSDirectionalWarnings(), Loc.LS("PLAYS HOMING WARNINGS FROM THE DIRECTION OF THE INCOMING PROJECTILE"));
            position.y += 62f;
            uie.SelectAndDrawItem("REINITIALIZE AUDIO DEVICE", position, 5, false);
        }

        // First SelectAndDrawStringOptionItem() after "SPEAKER MODE" string, draw our menu
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 93f)
                {
                    code.operand = 186f; // offsets the menu up a bit for the extra options
                }
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "SPEAKER MODE")
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "SelectAndDrawStringOptionItem"))
                {
                    state = 2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawSoundMenu), "DrawAdditionalSoundOptions"));
                    continue;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// https://github.com/overload-development-community/olmod/issues/148
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), "SoundOptionsUpdate")]
    class Menus_MenuManager_SoundOptionsUpdate
    {
        private static void AdditionalSoundOptions(int menu_selection)
        {
            switch (menu_selection)
            {
                case 5:
                    AudioSettings.Reset(AudioSettings.GetConfiguration());
                    MenuManager.PlaySelectSound(1f);
                    break;
                case 6:
                    Menus.mms_audio_occlusion_strength = (Menus.mms_audio_occlusion_strength + UIManager.m_select_dir) % 4;
                    if (Menus.mms_audio_occlusion_strength < 0)
                    {
                        Menus.mms_audio_occlusion_strength = 3;
                    }
                    MenuManager.PlaySelectSound(1f);
                    break;
                case 7:
                    Menus.mms_directional_warnings = !Menus.mms_directional_warnings;
                    MenuManager.PlaySelectSound(1f);
                    break;

            }
        }

        // Handle menu selection just before MenuManager.UnReverseOption
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(MenuManager), "UnReverseOption"))
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_1) { labels = code.labels };
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_SoundOptionsUpdate), "AdditionalSoundOptions"));
                    code.labels = null;
                }
                yield return code;
            }
        }
    }

        // Fix next/previous resolution buttons.
        [HarmonyPatch(typeof(MenuManager), "SelectNextResolution")]
    class FixSelectNextResolution {
        static bool Prefix() {
            var resolutions = Screen.resolutions.Where(r => r.width >= 800 && r.height >= 540).Select(r => new Resolution { width = r.width, height = r.height }).Distinct().ToList();

            resolutions.Sort((a, b) => {
                return a.width == b.width ? a.height - b.height : a.width - b.width;
            });

            var index = resolutions.IndexOf(new Resolution { width = MenuManager.m_resolution_width, height = MenuManager.m_resolution_height });

            if (index == -1) {
                index = resolutions.Count() - 1;
            } else if (UIManager.m_select_dir > 0) {
                index++;
                if (index >= resolutions.Count()) {
                    index = 0;
                }
            } else {
                index--;
                if (index < 0) {
                    index = resolutions.Count() - 1;
                }
            }

            MenuManager.m_resolution_width = resolutions[index].width;
            MenuManager.m_resolution_height = resolutions[index].height;

            return false;
        }
    }

    // Patch in Change Team option in the Pause UI
    [HarmonyPatch(typeof(UIElement), "DrawPauseMenu")]
    class Menus_UIElement_DrawPauseMenu
    {

        // Only called in MP matches
        static void DrawMpTeamSwitch(UIElement uie, ref Vector2 position)
        {
            if (!NetworkMatch.IsTeamMode(MPModPrivateData.MatchMode) || !MPModPrivateData.JIPEnabled)
                return;

            // Specifying colors gets a bit goofy when using relative us/enemy assignments
            string selection;
            if (MPTeams.NetworkMatchTeamCount > 2)
            {
                selection = MPTeams.TeamName(Menus.mms_team_selection ?? GameManager.m_local_player.m_mp_team);
            }
            else
            {
                selection = (Menus.mms_team_selection ?? GameManager.m_local_player.m_mp_team) == GameManager.m_local_player.m_mp_team ? "CURRENT" : "SWITCH";
            }
            uie.SelectAndDrawStringOptionItem("TEAM", position, 13, selection, "", 1f, false);

            position.y += 62f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "QUIT TO MENU")
                {
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0); // Vector2 position
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawPauseMenu), "DrawMpTeamSwitch"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0); // We stole 'this' to pass as first arg to DrawMpTeamSwitch, put back on stack
                }

                yield return code;
            }
        }
    }

    // Menu handler for Change Team option in pause menu
    [HarmonyPatch(typeof(MenuManager), "PausedUpdate")]
    class Menus_MenuManager_PausedUpdate
    {
        static void HandleMenuSelection()
        {
            if (UIManager.m_menu_selection == 13)
            {
                Menus.mms_team_selection = MPTeams.NextTeam(Menus.mms_team_selection ?? GameManager.m_local_player.m_mp_team);
                MenuManager.PlaySelectSound(1f);
            }
        }

        static void InitializeMenu()
        {
            Menus.mms_team_selection = null;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(PilotManager), "Save"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_PausedUpdate), "InitializeMenu"));
                    continue;
                }

                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(UIManager), "m_menu_selection"))
                {
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_PausedUpdate), "HandleMenuSelection")) { labels = code.labels };
                    code.labels = null;
                }

                yield return code;
            }
        }
    }

    // On resume from MP Pause menu, check to see if the current team selection has changed and if so send it to the server
    [HarmonyPatch(typeof(MenuManager), "ResumeFromPauseMenu")]
    class Menus_MenuManager_ResumeFromPauseMenu
    {
        static void Postfix()
        {
            if (!GameplayManager.IsMultiplayer || !NetworkMatch.IsTeamMode(MPModPrivateData.MatchMode) || !MPModPrivateData.JIPEnabled)
                return;

            if (Menus.mms_team_selection.HasValue && Menus.mms_team_selection != GameManager.m_local_player.m_mp_team)
            {
                Client.GetClient().Send(MessageTypes.MsgChangeTeam, new MPTeams.ChangeTeamMessage { netId = GameManager.m_local_player.netId, newTeam = Menus.mms_team_selection.Value });
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "HUDOptionsUpdate")]
    class Menus_MenuManager_HUDOptionsUpdate
    {
        private static MethodInfo _MenuManager_GoBack_Method = AccessTools.Method(typeof(MenuManager), "GoBack");
        private static MethodInfo _MenuManager_PlayHighlightSound_Method = AccessTools.Method(typeof(MenuManager), "PlayHighlightSound");
        private static FieldInfo _MenuManager_m_menu_state_timer_Field = AccessTools.Field(typeof(MenuManager), "m_menu_state_timer");

        static bool Prefix()
        {
            MenuManager.UpdateMPStatus();
            UIManager.MouseSelectUpdate();
            MenuSubState menu_sub_state = MenuManager.m_menu_sub_state;
            if (menu_sub_state != MenuSubState.INIT)
            {
                if (menu_sub_state == MenuSubState.ACTIVE)
                {
                    UIManager.ControllerMenu();
                    if (Controls.JustPressed(CCInput.MENU_SECONDARY))
                    {
                        MenuManager.m_mp_status_minimized = !MenuManager.m_mp_status_minimized;
                        MenuManager.PlayCycleSound(1f, 1f);
                    }
                    if (Controls.JustPressed(CCInput.MENU_PGUP) || (UIManager.PushedSelect(-1) && UIManager.m_menu_selection == 198))
                    {
                        _MenuManager_PlayHighlightSound_Method.Invoke(null, new object[] { 0.4f, 0.05f });
                        MenuManager.UIPulse(1f);
                        MenuManager.m_menu_micro_state = 1 - MenuManager.m_menu_micro_state;
                    }
                    else if (Controls.JustPressed(CCInput.MENU_PGDN) || (UIManager.PushedSelect(-1) && UIManager.m_menu_selection == 199))
                    {
                        _MenuManager_PlayHighlightSound_Method.Invoke(null, new object[] { 0.4f, 0.05f });
                        MenuManager.UIPulse(1f);
                        MenuManager.m_menu_micro_state = 1 - MenuManager.m_menu_micro_state;
                    }
                    else
                    {
                        if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()))
                        {
                            MenuManager.MaybeReverseOption();
                            int menu_selection = UIManager.m_menu_selection;
                            switch (menu_selection)
                            {
                                case 0:
                                    MenuManager.opt_hud_color = (MenuManager.opt_hud_color + 5 + UIManager.m_select_dir) % 5;
                                    UIManager.UpdateUIColors(MenuManager.opt_hud_color);
                                    Robot.GuidebotFadeAmt -= 0.01f;
                                    AutomapMarker.m_update_color = true;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 1:
                                    MenuManager.opt_hud_reticle = (MenuManager.opt_hud_reticle + 3 + UIManager.m_select_dir) % 3;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 2:
                                    MenuManager.opt_hud_weapons = (MenuManager.opt_hud_weapons + 2 + UIManager.m_select_dir) % 2;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 3:
                                    MenuManager.opt_hud_sway = (MenuManager.opt_hud_sway + 3 + UIManager.m_select_dir) % 3;
                                    GameManager.m_player_ship.ResetCameraSway(false);
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 4:
                                    GameManager.m_player_ship.ToggleCockpitVisible();
                                    if (GameManager.m_player_ship.c_player.m_cloaked)
                                    {
                                        GameManager.m_player_ship.CloakUpdateCockpit();
                                    }
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 6:
                                    MenuManager.opt_hud_banner_messages = (MenuManager.opt_hud_banner_messages + 2 + UIManager.m_select_dir) % 2;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 7:
                                    MenuManager.opt_text_for_audio = (MenuManager.opt_text_for_audio + 2 + UIManager.m_select_dir) % 2;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 8:
                                    MenuManager.opt_speedrun_timers = !MenuManager.opt_speedrun_timers;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 9:
                                    MenuManager.opt_hud_shake = (MenuManager.opt_hud_shake + 3 + UIManager.m_select_dir) % 3;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 10:
                                    MenuManager.opt_hud_flicker = (MenuManager.opt_hud_flicker + 3 + UIManager.m_select_dir) % 3;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 11:
                                    RearView.Toggle();
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 12:
                                    HUDVelocity.MenuManagerEnabled = !HUDVelocity.MenuManagerEnabled;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                case 13:
                                    Menus.mms_show_framerate = !Menus.mms_show_framerate;
                                    GameManager.m_display_fps = Menus.mms_show_framerate;
                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                    break;
                                default:
                                    if (menu_selection == 100)
                                    {
                                        _MenuManager_GoBack_Method.Invoke(null, null);
                                        UIManager.DestroyAll(false);
                                        MenuManager.PlaySelectSound(1f);
                                    }
                                    break;
                            }
                            MenuManager.UnReverseOption();
                        }
                    }                    
                }
            }
            else if ((float)_MenuManager_m_menu_state_timer_Field.GetValue(null) > 0.25f)
            {
                UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.HUD_MENU);
                MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                MenuManager.SetDefaultSelection(0);
            }
            else
            {
                MenuManager.m_menu_micro_state = 0;
            }

            return false;
        }

        [HarmonyPatch(typeof(UIElement), "DrawMpCustomize")]
        internal class Menus_UIElement_DrawMpCustomize
        {
            private static MethodInfo _UIElement_DrawXPTotalMedium_Method = AccessTools.Method(typeof(UIElement), "DrawXPTotalMedium");

            static bool Prefix(UIElement __instance)
            {
                UIManager.X_SCALE = 0.2f;
                UIManager.ui_bg_dark = true;
                __instance.DrawMenuBG();
                Vector2 position = __instance.m_position;
                position.y = UIManager.UI_TOP + 60f;
                __instance.DrawHeaderLarge(position, Loc.LS("CUSTOMIZATION"), 600f);
                position.y += 50f;
                __instance.DrawMpTabs(position, MenuManager.m_menu_micro_state);
                position.y += 45f;
                _UIElement_DrawXPTotalMedium_Method.Invoke(__instance, new object[] { position });
                position.y += 95f;
                __instance.DrawMenuSeparator(position - Vector2.up * 40f);
                Player local_player = GameManager.m_local_player;
                int menu_micro_state = MenuManager.m_menu_micro_state;
                if (menu_micro_state != 0)
                {
                    if (menu_micro_state != 1)
                    {
                        if (menu_micro_state == 2)
                        {
                            position.x = -320f;
                            position.y = -115f;
                            __instance.SelectAndDrawStringOptionItem(Loc.LS("VERSION"), position, 0, MenuManager.GetMpTeamName(MenuManager.mpc_ship_version), string.Empty, 1.2f, false);
                            position.y += 62f;
                            __instance.SelectAndDrawStringOptionItem(Loc.LS("GLOW COLOR"), position, 1, MenuManager.GetMpGlowColor(), string.Empty, 1.2f, MenuManager.mpc_ship_version != MpTeam.ANARCHY);
                            position.y += 62f;
                            __instance.SelectAndDrawStringOptionItem(Loc.LS("DECAL COLOR"), position, 2, MenuManager.GetMpDecalColor(), string.Empty, 1.2f, MenuManager.mpc_ship_version != MpTeam.ANARCHY);
                            position.y += 62f;
                            __instance.SelectAndDrawStringOptionItem(Loc.LS("DECAL TYPE"), position, 3, MenuManager.GetMpDecalPattern(), string.Empty, 1.2f, false);
                            position.y += 62f;
                            __instance.SelectAndDrawStringOptionItem(Loc.LS("WINGS"), position, 4, MenuManager.GetMpCustomWings(), string.Empty, 1.2f, false);
                            position.y += 62f;
                            __instance.SelectAndDrawStringOptionItem(Loc.LS("BODY"), position, 5, MenuManager.GetMpCustomBody(), string.Empty, 1.2f, false);
                            position.y = 40f;
                            position.x = 340f;
                            __instance.TestMouseInRectSlider(position, 300f, 150f, 10, false);
                            Color c = (UIManager.m_menu_selection != 10) ? UIManager.m_col_ub0 : UIManager.m_col_ui2;
                            c.a = __instance.m_alpha;
                            UIManager.DrawFrameEmptyCenter(position, 14f, 14f, 524f, 254f, c, 8);
                            UIManager.PauseMainDrawing();
                            UIManager.StartDrawing(UIManager.url[1], true, 750f);
                            c = HSBColor.ConvertToColor(0f, 0f, 0.8f);
                            UIManager.DrawTileFull(position, 270f, 135f, c, __instance.m_alpha * UnityEngine.Random.Range(0.75f, 0.8f));
                            UIManager.ResumeMainDrawing();
                        }
                    }
                    else
                    {
                        position.y = -153f;
                        __instance.DrawLabelSmall(position, Loc.LS("SELECT ONE MODIFIER FROM EACH COLUMN"), 400f, 20f, 1f);
                        position.x = -310f;
                        position.y = -90f;
                        for (int i = 0; i < 4; i++)
                        {
                            __instance.DrawMpModifier(position, i, true, local_player);
                            position.y += 95f;
                        }
                        position.x = 310f;
                        position.y = -90f;
                        for (int j = 0; j < 4; j++)
                        {
                            __instance.DrawMpModifier(position, j, false, local_player);
                            position.y += 95f;
                        }
                    }
                }
                else
                {
                    position.y = -153f;
                    __instance.DrawLabelSmall(position, Loc.LS("SELECT YOUR LOADOUT WEAPONS (REFLEX SIDEARM INCLUDED IN ALL LOADOUTS)"), 400f, 20f, 1f);
                    position.x = -310f;
                    position.y = -90f;

                    DrawMpLoadout(__instance, position, 0);
                    position.y += 95f;
                    DrawMpLoadout(__instance, position, 2);
                    position.x = 310f;
                    position.y = -90f;
                    DrawMpLoadout(__instance, position, 1);
                    position.y += 95f;
                    DrawMpLoadout(__instance, position, 3);
                }
                position.x = 0f;
                position.y = 280f;
                __instance.DrawMenuSeparator(position - Vector2.up * 30f);
                __instance.DrawPageControls(position - Vector2.up * 10f, string.Empty, true, true, false, false, 410, 544, false);
                position.y = UIManager.UI_BOTTOM - 30f;
                __instance.SelectAndDrawItem(Loc.LS("BACK"), position, 100, false, 1f, 0.75f);
                __instance.MaybeShowMpStatus();

                return false;
            }

            private static void DrawMpLoadout(UIElement __instance, Vector2 pos, int idx)
            {
                float num = 535f;
                float middle_h = 55f;
                Color col_ub = UIManager.m_col_ub0;
                col_ub.a = __instance.m_alpha;
                UIManager.DrawFrameEmptyCenter(pos, 17f, 17f, num, middle_h, col_ub, 7);
                int loadoutMinXP = Player.GetLoadoutMinXP(idx);
                __instance.SelectAndDrawCheckboxItem(GetMpLoadoutName(idx), pos - Vector2.up * 10f, idx, true, false, 1f, -1);
                pos.y += 28f;

                __instance.DrawStringSmall(Loc.LS("CUSTOMIZABLE"), pos - Vector2.up * 38f + Vector2.right * 230f, 0.5f, StringOffset.RIGHT, UIManager.m_col_ui0, 0.5f, -1f);
                num *= 0.345f;
                pos.x -= num;

                if (idx % 2 == 0)
                {
                    WeaponType weaponType = GetMpLoadoutWeapon(idx, 0);
                    __instance.SelectAndDrawMicroItem(Player.WeaponNames[(int)weaponType], pos, 10 + 3 * idx, false, (int)(26 + weaponType), 0.31f);
                    pos.x += num;
                    MissileType missileType = GetMpLoadoutMissile(idx, 0);
                    __instance.SelectAndDrawMicroItem(Player.MissileNames[(int)missileType], pos, 11 + 3 * idx, false, (int)(104 + missileType), 0.31f);
                    pos.x += num;
                    missileType = GetMpLoadoutMissile(idx, 1);
                    __instance.SelectAndDrawMicroItem(Player.MissileNames[(int)missileType], pos, 12 + 3 * idx, false, (int)(104 + missileType), 0.31f);
                }
                else
                {
                    WeaponType weaponType = GetMpLoadoutWeapon(idx, 0);
                    __instance.SelectAndDrawMicroItem(Player.WeaponNames[(int)weaponType], pos, 13 + 3 * (idx - 1), false, (int)(26 + weaponType), 0.31f);
                    pos.x += num;
                    weaponType = GetMpLoadoutWeapon(idx, 1);
                    __instance.SelectAndDrawMicroItem(Player.WeaponNames[(int)weaponType], pos, 14 + 3 * (idx - 1), false, (int)(26 + weaponType), 0.31f);
                    pos.x += num;
                    MissileType missileType = GetMpLoadoutMissile(idx, 0);
                    __instance.SelectAndDrawMicroItem(Player.MissileNames[(int)missileType], pos, 15 + 3 * (idx - 1), false, (int)(104 + missileType), 0.31f);
                }
            }

            private static string GetMpLoadoutName(int idx)
            {
                switch (idx)
                {
                    case 0:
                        return "BOMBER 1";
                    case 1:
                        return "GUNNER 1";
                    case 2:
                        return "BOMBER 2";
                    case 3:
                        return "GUNNER 2";
                    default:
                        return "UNKNOWN";
                }
            }

            private static WeaponType GetMpLoadoutWeapon(int loadoutIndex, int weaponIndex)
            {
                return MPLoadouts.Loadouts[loadoutIndex].weapons[weaponIndex];
            }

            private static MissileType GetMpLoadoutMissile(int loadoutIndex, int missileIndex)
            {
                return MPLoadouts.Loadouts[loadoutIndex].missiles[missileIndex];
            }
        }

        [HarmonyPatch(typeof(MenuManager), "MpCustomizeUpdate")]
        internal class Menus_MenuManager_MpCustomizeUpdate
        {
            private static MethodInfo _MenuManager_GoBack_Method = AccessTools.Method(typeof(MenuManager), "GoBack");
            private static MethodInfo _MenuManager_PlayHighlightSound_Method = AccessTools.Method(typeof(MenuManager), "PlayHighlightSound");
            private static FieldInfo _MenuManager_m_briefing_object_Field = AccessTools.Field(typeof(MenuManager), "m_briefing_object");
            private static FieldInfo _MenuManager_m_briefing_object_ready_Field = AccessTools.Field(typeof(MenuManager), "m_briefing_object_ready");
            private static FieldInfo _MenuManager_m_menu_state_timer_Field = AccessTools.Field(typeof(MenuManager), "m_menu_state_timer");
            private static MethodInfo _MenuManager_SetBriefingObject_Method = AccessTools.Method(typeof(MenuManager), "SetBriefingObject");
            private static MethodInfo _MenuManager_SetEntityBriefingVars_Method = AccessTools.Method(typeof(MenuManager), "SetEntityBriefingVars");


            private static void PlayHighlightSound(float vol = 1f, float pitch = 0f)
            {
                _MenuManager_PlayHighlightSound_Method.Invoke(null, new object[] { vol, pitch });
            }

            private static void GoBack()
            {
                _MenuManager_GoBack_Method.Invoke(null, new object[] { });
            }

            private static GameObject m_briefing_object
            {
                get
                {
                    return (GameObject)_MenuManager_m_briefing_object_Field.GetValue(null);
                }
            }

            private static float m_menu_state_timer
            {
                get
                {
                    return (float)_MenuManager_m_menu_state_timer_Field.GetValue(null);
                }
            }

            private static bool m_briefing_object_ready
            {
                get
                {
                    return (bool)_MenuManager_m_briefing_object_ready_Field.GetValue(null);
                }
                set
                {
                    _MenuManager_m_briefing_object_ready_Field.SetValue(null, value);
                }
            }

            private static void SetBriefingObject(MenuManager.EntityBriefingType entity_type, int entity_num)
            {
                _MenuManager_SetBriefingObject_Method.Invoke(null, new object[] { entity_type, entity_num });
            }

            private static void SetEntityBriefingVars()
            {
                _MenuManager_SetEntityBriefingVars_Method.Invoke(null, new object[] { });
            }

            static bool Prefix()
            {
                UIManager.MouseSelectUpdate();
                MenuManager.UpdateMPStatus();
                MenuSubState menu_sub_state = MenuManager.m_menu_sub_state;
                if (menu_sub_state != MenuSubState.INIT)
                {
                    if (menu_sub_state == MenuSubState.ACTIVE)
                    {
                        UIManager.ControllerMenu();
                        if (Controls.JustPressed(CCInput.MENU_PGUP) || (UIManager.PushedSelect(-1) && UIManager.m_menu_selection == 198))
                        {
                            MenuManager.m_menu_micro_state = (MenuManager.m_menu_micro_state + 2) % 3;
                            MenuManager.UIPulse(1f);
                            PlayHighlightSound(0.4f, 0.05f);
                        }
                        else if (Controls.JustPressed(CCInput.MENU_PGDN) || (UIManager.PushedSelect(-1) && UIManager.m_menu_selection == 199))
                        {
                            MenuManager.m_menu_micro_state = (MenuManager.m_menu_micro_state + 1) % 3;
                            MenuManager.UIPulse(1f);
                            PlayHighlightSound(0.4f, 0.07f);
                        }
                        else
                        {
                            Player local_player = GameManager.m_local_player;
                            if (Controls.JustPressed(CCInput.MENU_SECONDARY))
                            {
                                MenuManager.m_mp_status_minimized = !MenuManager.m_mp_status_minimized;
                                MenuManager.PlayCycleSound(1f, 1f);
                            }
                            if (MenuManager.m_menu_micro_state == 2)
                            {
                                if (m_briefing_object != null && !m_briefing_object_ready)
                                {
                                    m_briefing_object.SetActive(true);
                                    m_briefing_object_ready = true;
                                }
                                if (!MenuManager.m_custom_rotation)
                                {
                                    MenuManager.m_custom_rot_amt = 45f * RUtility.FRAMETIME_UI;
                                }
                                if (Input.GetMouseButtonDown(1))
                                {
                                    MenuManager.m_custom_rotation = false;
                                }
                                if (Input.GetMouseButtonDown(0))
                                {
                                    UIElement.SliderDiff = 0f;
                                    MenuManager.m_custom_rotation = true;
                                }
                                if (UIElement.SliderValid && Input.GetMouseButton(0))
                                {
                                    MenuManager.m_custom_rotation = true;
                                    MenuManager.m_custom_rot_amt = UIElement.SliderDiff * -180f;
                                }
                                m_briefing_object.transform.Rotate(new Vector3(0f, MenuManager.m_custom_rot_amt, 0f));
                            }
                            if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()))
                            {
                                MenuManager.MaybeReverseOption();
                                int menu_micro_state = MenuManager.m_menu_micro_state;
                                if (menu_micro_state != 0)
                                {
                                    if (menu_micro_state != 1)
                                    {
                                        if (menu_micro_state == 2)
                                        {
                                            int menu_selection = UIManager.m_menu_selection;
                                            switch (menu_selection)
                                            {
                                                case 0:
                                                    MenuManager.mpc_ship_version = (MpTeam)(((int)MenuManager.mpc_ship_version + 3 + UIManager.m_select_dir) % (int)MpTeam.NUM_TEAMS);
                                                    MenuManager.MpUpdateShipColors(m_briefing_object, MenuManager.mpc_ship_version, MenuManager.mpc_glow_color, MenuManager.mpc_decal_color, MenuManager.mpc_decal_pattern);
                                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                                    break;
                                                case 1:
                                                    MenuManager.mpc_glow_color = (MenuManager.mpc_glow_color + 9 + UIManager.m_select_dir) % 9;
                                                    MenuManager.MpUpdateShipColors(m_briefing_object, MenuManager.mpc_ship_version, MenuManager.mpc_glow_color, MenuManager.mpc_decal_color, MenuManager.mpc_decal_pattern);
                                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                                    break;
                                                case 2:
                                                    MenuManager.mpc_decal_color = (MenuManager.mpc_decal_color + 11 + UIManager.m_select_dir) % 11;
                                                    MenuManager.MpUpdateShipColors(m_briefing_object, MenuManager.mpc_ship_version, MenuManager.mpc_glow_color, MenuManager.mpc_decal_color, MenuManager.mpc_decal_pattern);
                                                    MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                                    break;
                                                case 3:
                                                    {
                                                        int num = GameManager.m_gm.m_mp_custom_decal.Length;
                                                        MenuManager.mpc_decal_pattern = (MenuManager.mpc_decal_pattern + num + UIManager.m_select_dir) % num;
                                                        MenuManager.MpUpdateShipDecal(m_briefing_object, MenuManager.mpc_decal_pattern);
                                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                                        break;
                                                    }
                                                case 4:
                                                    {
                                                        int num = GameManager.m_gm.m_mp_custom_mesh_wings.Length + 1;
                                                        MenuManager.mpc_mesh_wings = (MenuManager.mpc_mesh_wings + num + UIManager.m_select_dir) % num;
                                                        MenuManager.MpUpdateShipWings(MenuManager.mpc_mesh_wings);
                                                        MenuManager.MpUpdateShipColors(m_briefing_object, MenuManager.mpc_ship_version, MenuManager.mpc_glow_color, MenuManager.mpc_decal_color, MenuManager.mpc_decal_pattern);
                                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                                        break;
                                                    }
                                                case 5:
                                                    {
                                                        int num = GameManager.m_gm.m_mp_custom_mesh_body.Length + 1;
                                                        MenuManager.mpc_mesh_body = (MenuManager.mpc_mesh_body + num + UIManager.m_select_dir) % num;
                                                        MenuManager.MpUpdateShipBody(MenuManager.mpc_mesh_body);
                                                        MenuManager.MpUpdateShipColors(m_briefing_object, MenuManager.mpc_ship_version, MenuManager.mpc_glow_color, MenuManager.mpc_decal_color, MenuManager.mpc_decal_pattern);
                                                        MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
                                                        break;
                                                    }
                                                default:
                                                    switch (menu_selection)
                                                    {
                                                        case 200:
                                                        case 201:
                                                        case 202:
                                                            MenuManager.m_menu_micro_state = UIManager.m_menu_selection - 200;
                                                            MenuManager.UIPulse(1f);
                                                            PlayHighlightSound(0.4f, 0.07f);
                                                            break;
                                                        default:
                                                            if (menu_selection == 100)
                                                            {
                                                                GoBack();
                                                                UIManager.DestroyAll(false);
                                                                MenuManager.PlaySelectSound(1f);
                                                            }
                                                            break;
                                                    }
                                                    break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        int menu_selection2 = UIManager.m_menu_selection;
                                        switch (menu_selection2)
                                        {
                                            case 0:
                                            case 1:
                                            case 2:
                                            case 3:
                                                Player.Mp_modifier1 = UIManager.m_menu_selection;
                                                Client.SendPlayerLoadoutToServer();
                                                MenuManager.PlayCycleSound(1f, 1f);
                                                break;
                                            case 4:
                                            case 5:
                                            case 6:
                                            case 7:
                                                Player.Mp_modifier2 = UIManager.m_menu_selection - 4;
                                                Client.SendPlayerLoadoutToServer();
                                                MenuManager.PlayCycleSound(1f, 1f);
                                                break;
                                            default:
                                                switch (menu_selection2)
                                                {
                                                    case 200:
                                                    case 201:
                                                    case 202:
                                                        MenuManager.m_menu_micro_state = UIManager.m_menu_selection - 200;
                                                        MenuManager.UIPulse(1f);
                                                        PlayHighlightSound(0.4f, 0.07f);
                                                        break;
                                                    default:
                                                        if (menu_selection2 == 100)
                                                        {
                                                            GoBack();
                                                            UIManager.DestroyAll(false);
                                                            MenuManager.PlaySelectSound(1f);
                                                        }
                                                        break;
                                                }
                                                break;
                                        }
                                    }
                                }
                                else
                                {
                                    int menu_selection3 = UIManager.m_menu_selection;
                                    switch (menu_selection3)
                                    {
                                        case 10:
                                            MPLoadouts.MpCycleWeapon(0, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 11:
                                            MPLoadouts.MpCycleMissile(0, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 12:
                                            MPLoadouts.MpCycleMissile(0, 1);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 13:
                                            MPLoadouts.MpCycleWeapon(1, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 14:
                                            MPLoadouts.MpCycleWeapon(1, 1);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 15:
                                            MPLoadouts.MpCycleMissile(1, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 16:
                                            MPLoadouts.MpCycleWeapon(2, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 17:
                                            MPLoadouts.MpCycleMissile(2, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 18:
                                            MPLoadouts.MpCycleMissile(2, 1);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 19:
                                            MPLoadouts.MpCycleWeapon(3, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 20:
                                            MPLoadouts.MpCycleWeapon(3, 1);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        case 21:
                                            MPLoadouts.MpCycleMissile(3, 0);
                                            MenuManager.PlayCycleSound(1f, 1f);
                                            break;
                                        default:
                                            switch (menu_selection3)
                                            {
                                                case 200:
                                                case 201:
                                                case 202:
                                                    MenuManager.m_menu_micro_state = UIManager.m_menu_selection - 200;
                                                    MenuManager.UIPulse(1f);
                                                    PlayHighlightSound(0.4f, 0.07f);
                                                    break;
                                                default:
                                                    if (menu_selection3 == 100)
                                                    {
                                                        GoBack();
                                                        UIManager.DestroyAll(false);
                                                        MenuManager.PlaySelectSound(1f);
                                                    }
                                                    break;
                                            }
                                            break;
                                    }
                                }
                                MenuManager.UnReverseOption();
                            }
                        }
                    }
                }
                else if (m_menu_state_timer > 0.25f)
                {
                    MenuManager.m_custom_rotation = false;
                    UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, UIElementType.MP_CUSTOMIZE);
                    MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
                    MenuManager.m_menu_micro_state = 0;
                    MenuManager.SetDefaultSelection(0);
                    MenuManager.m_entity_briefing_type = MenuManager.EntityBriefingType.PlayerShip;
                    MenuManager.m_entity_num = 0;
                    SetEntityBriefingVars();
                    SetBriefingObject(MenuManager.m_entity_briefing_type, MenuManager.m_entity_num);
                    GameManager.m_player_ship.c_viewer.SetLensFlares(false);
                    UIManager.SetTexture(GameplayManager.m_gm.m_briefing_camera.targetTexture);
                    MenuManager.mpc_mesh_body = Mathf.Clamp(MenuManager.mpc_mesh_body, 0, GameManager.m_gm.m_mp_custom_mesh_body.Length);
                    MenuManager.mpc_mesh_wings = Mathf.Clamp(MenuManager.mpc_mesh_wings, 0, GameManager.m_gm.m_mp_custom_mesh_wings.Length);
                    MenuManager.MpUpdateShipWings(MenuManager.mpc_mesh_wings);
                    MenuManager.MpUpdateShipBody(MenuManager.mpc_mesh_body);
                    MenuManager.MpUpdateShipColors(m_briefing_object, MenuManager.mpc_ship_version, MenuManager.mpc_glow_color, MenuManager.mpc_decal_color, MenuManager.mpc_decal_pattern);
                    MenuManager.MpUpdateShipDecal(m_briefing_object, MenuManager.mpc_decal_pattern);
                }

                return false;
            }
        }
    }

    /// <summary>
    /// Draw additional CCInputExt bindings under Controls -> Additional Controls page under Re-center VR
    /// </summary>
    [HarmonyPatch(typeof(UIElement), "DrawControlsMenu")]
    internal class Menus_UIElement_DrawControlsMenu
    {
        static void DrawAdditionalBindings(UIElement uie, Vector2 position, bool joystick)
        {
            uie.SelectAndDrawControlOption("TOGGLE LOADOUT PRIMARY", position, (int)CCInputExt.TOGGLE_LOADOUT_PRIMARY, joystick);
            position.y += 48f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int control_remap_page2_count = 0;
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(MenuManager), "control_remap_page2"))
                {
                    control_remap_page2_count++;
                    state = 0;
                }

                if ((control_remap_page2_count == 1 || control_remap_page2_count == 3) && code.opcode == OpCodes.Ldloca_S)
                {
                    state++;

                    if (state == 4)
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0) { labels = code.labels };
                        yield return new CodeInstruction(OpCodes.Ldloc_0);
                        yield return new CodeInstruction(OpCodes.Ldc_I4, control_remap_page2_count);
                        yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                        yield return new CodeInstruction(OpCodes.Ceq); // control_remap_page2_count == 1 is joystick branch, otherwise MKB
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawControlsMenu), "DrawAdditionalBindings"));
                        code.labels = null;
                    }
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Process changes to menu bindings. Most work focuses around sticking point of original code using CCInput + 50 value for the
    /// "alt" representation and having to adjust to account for new higher CCInputExt values
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), "ControlsOptionsUpdate")]
    internal class Menus_MenuManager_ControlsOptionsUpdate
    {
        static void AdjustRemapValues()
        {
            MenuManager.control_remap_alt = UIManager.m_menu_selection >= 1000;
            MenuManager.control_remap_index = UIManager.m_menu_selection % 1000;
            if ((UIManager.m_menu_selection >= (int)CCInputExt.TOGGLE_LOADOUT_PRIMARY && UIManager.m_menu_selection < 1000)
                || UIManager.m_menu_selection >= (int)CCInputExt.TOGGLE_LOADOUT_PRIMARY + 1000)
            {
                MenuManager.control_remap_name = ControlsExt.GetInputName((CCInputExt)(UIManager.m_menu_selection % 1000));
            }
        }

        static void SetInputKB(int idx, bool alt, KeyCode kc)
        {
            // Previously in MPAnticheat.cs Prefix
            if (!(idx != 14 && idx != 15 || kc != KeyCode.Joystick8Button10 && kc != KeyCode.Joystick8Button11))
                return;

            int exclusionMask = ControlsExt.GetExclusionMask((CCInputExt)idx);
            for (int i = 0; i < 45; i++)
            {
                if ((exclusionMask & ControlsExt.GetExclusionMask((CCInputExt)i)) != 0)
                {
                    if (Controls.m_input_kc[0, i] == kc)
                    {
                        Controls.m_input_kc[0, i] = KeyCode.None;
                    }
                    if (Controls.m_input_kc[1, i] == kc)
                    {
                        Controls.m_input_kc[1, i] = KeyCode.None;
                    }
                }
            }
            for (int i = (int)CCInputExt.TOGGLE_LOADOUT_PRIMARY; i < ControlsExt.MAX_ARRAY_SIZE; i++)
            {
                if ((exclusionMask & ControlsExt.GetExclusionMask((CCInputExt)i)) != 0)
                {
                    if (Controls.m_input_kc[0, i] == kc)
                    {
                        Controls.m_input_kc[0, i] = KeyCode.None;
                    }
                    if (Controls.m_input_kc[1, i] == kc)
                    {
                        Controls.m_input_kc[1, i] = KeyCode.None;
                    }
                }
            }
            Controls.m_input_kc[(!alt) ? 0 : 1, idx] = kc;
        }

        private static void AdjustMenuSelection()
        {
            UIManager.m_menu_selection = MenuManager.control_remap_index + ((!MenuManager.control_remap_alt) ? 0 : 1000);
        }

        private static void ResetControlKB(int idx, int slot)
        {
            var _idx = UIManager.m_menu_selection % 1000;
            var _slot = UIManager.m_menu_selection < 1000 ? 0 : 1;
            Controls.ResetControlKB(_idx, _slot);
        }

        private static void ResetControlJoy(int idx, int slot)
        {
            var _idx = UIManager.m_menu_selection % 1000;
            var _slot = UIManager.m_menu_selection < 1000 ? 0 : 1;
            Controls.ResetControlJoy(_idx, _slot);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(MenuManager), "control_remap_name"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_ControlsOptionsUpdate), "AdjustRemapValues"));
                    continue;
                }

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Controls), "SetInputKB"))
                    code.operand = AccessTools.Method(typeof(Menus_MenuManager_ControlsOptionsUpdate), "SetInputKB");

                if (code.opcode == OpCodes.Stsfld && code.operand == AccessTools.Field(typeof(UIManager), "m_menu_selection"))
                {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_ControlsOptionsUpdate), "AdjustMenuSelection"));
                    continue;
                }

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Controls), "ResetControlKB"))
                    code.operand = AccessTools.Method(typeof(Menus_MenuManager_ControlsOptionsUpdate), "ResetControlKB");

                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Controls), "ResetControlJoy"))
                    code.operand = AccessTools.Method(typeof(Menus_MenuManager_ControlsOptionsUpdate), "ResetControlJoy");

                yield return code;
            }
        }
    }
}
