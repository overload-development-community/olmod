using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    public static class Menus
    {

        //public static MenuState msServerBrowser = (MenuState)75;
        public static MenuState msLagCompensation = (MenuState)76;
        public static MenuState msAutoSelect = (MenuState)77;
        public static MenuState msAxisCurveEditor = (MenuState)78;
        public static MenuState msTeamColors = (MenuState)79;
        //public static UIElementType uiServerBrowser = (UIElementType)89;
        public static UIElementType uiLagCompensation = (UIElementType)90;
        public static UIElementType uiAutoSelect = (UIElementType)91;
        public static UIElementType uiAxisCurveEditor = (UIElementType)92;
        public static UIElementType uiTeamColors = (UIElementType)93;
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
        public static bool mms_assist_scoring { get; set; } = true;
        public static bool mms_team_color_default { get; set; } = true;
        public static int mms_team_color_self = 5;
        public static int mms_team_color_enemy = 6;

        public static string GetMMSRearViewPIP()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(RearView.MPMenuManagerEnabled));
        }

        public static string GetMMSAlwaysCloaked()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_always_cloaked));
        }

        public static string GetMMSAllowSmash()
        {
            return MenuManager.GetToggleSetting(Convert.ToInt32(mms_allow_smash));
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
        public static string mms_mp_projdata_fn = "STOCK";
        public static bool mms_sticky_death_summary = false;
        public static int mms_damageeffect_alpha_mult = 30;
        public static int mms_damageeffect_drunk_blur_mult = 10;
        public static int mms_match_time_limit = 60;
        public static bool mms_reduced_ship_explosions = true;
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
            uie.SelectAndDrawStringOptionItem(Loc.LS("ALWAYS CLOAKED"), position, 15, Menus.GetMMSAlwaysCloaked(), Loc.LS("SHIPS ARE ALWAYS CLOAKED"), 1f, false);
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("CLASSIC SPAWNS"), position, 13, Menus.GetMMSClassicSpawns(), Loc.LS("SPAWN WITH IMPULSE+ DUALS AND FALCONS"), 1f, false);
            position.y += 62f;
            // We're out of space, and assists don't matter in CTF anyway...
            if (MenuManager.mms_mode == ExtMatchMode.CTF)
            {
                uie.SelectAndDrawStringOptionItem(Loc.LS("CTF CARRIER BOOSTING"), position, 14, Menus.GetMMSCtfCarrierBoost(), Loc.LS("FLAG CARRIER CAN USE BOOST IN CTF"), 1f, false);
            }
            else
            {
                uie.SelectAndDrawStringOptionItem(Loc.LS("ASSISTS"), position, 18, Menus.GetMMSAssistScoring(), Loc.LS("AWARD POINTS FOR ASSISTING WITH KILLS"), 1f, false);
            }
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("PROJECTILE DATA"), position, 16, Menus.mms_mp_projdata_fn == "STOCK" ? "STOCK" : System.IO.Path.GetFileName(Menus.mms_mp_projdata_fn), string.Empty, 1f, false);
            position.y += 62f;
            if (DateTime.Now > new DateTime(2021, 4, 2))
            {
                uie.SelectAndDrawStringOptionItem(Loc.LS("ALLOW SMASH ATTACK"), position, 17, Menus.GetMMSAllowSmash(), Loc.LS("ALLOWS PLAYERS TO USE THE SMASH ATTACK"), 1f, false);
                position.y += 62f;
            }
        }

        private static void AdjustAdvancedPositionCenterColumn(ref Vector2 position)
        {
            position.x -= 300f;
            position.y = col_bot;
        }

        private static void DrawTeamSettings(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawItem("TEAM SETTINGS", position, 19, false, 1f, 0.75f);
            position.y += 62f;
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
        static void DrawMoreOptions(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawStringOptionItem(Loc.LS("STICKY DEATH SUMMARY"), position, 8, Menus.mms_sticky_death_summary ? "YES" : "NO", "KEEP DEATH SUMMARY ON THE SCREEN AFTER LETTING GO OF THE TOGGLE");
            position.y += 56f;
            uie.SelectAndDrawSliderItem(Loc.LS("DAMAGE BLUR INTENSITY"), position, 9, ((float)Menus.mms_damageeffect_drunk_blur_mult) / 100f);
            position.y += 56f;
            uie.SelectAndDrawSliderItem(Loc.LS("DAMAGE COLOR INTENSITY"), position, 10, ((float)Menus.mms_damageeffect_alpha_mult) / 100f);
            position.y += 56f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("SHIP EXPLOSION EFFECTS"), position, 12, Menus.mms_reduced_ship_explosions ? Loc.LS("REDUCED") : Loc.LS("FULL"), Loc.LS("REDUCED VISUAL CLUTTER DURING DEATH ROLL"));
            position.y += 56f;
            uie.SelectAndDrawItem(Loc.LS("TEAM COLORS"), position, 11, false, 1f, 0.75f);
            position.y += 56f;
            uie.SelectAndDrawItem(Loc.LS("LAG COMPENSATION SETTINGS"), position, 6, false, 1f, 0.75f);
            position.y += 56f;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldc_R4) {
                    if ((float)code.operand == 155f) {
                        code.operand = 300f;
                    }

                    if ((float)code.operand == 62f) {
                        code.operand = 56f;
                    }
                }
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "QUICK CHAT")
                {
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawMpOptions), "DrawMoreOptions"));
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                }

                // Remove deprecated "Cross-Platform Play" option
                // Skip block of code starting at first DrawMenuSeparator call through position.y += 62f
                if (state == 0 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "DrawMenuSeparator"))
                {
                    state = 1;
                    yield return code;
                    continue;
                }

                if (state == 1)
                {
                    if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(Vector2), "y"))
                        state = 2;
                    continue;
                }

                // Remove deprecated "Cross-Platform Chat" option
                // Skip starting at the ldstr call (preserving ldarg_0 for next call) and ending at the next ldarg_0 for Auto-Respawn Timer
                if (state == 2 && code.opcode == OpCodes.Ldstr && (string)code.operand == "CROSS-PLATFORM CHAT")
                {
                    state = 3;
                }

                if (state == 3)
                {
                    if (code.opcode == OpCodes.Ldarg_0)
                        state = 4;
                    continue;
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpOptionsUpdate")]
    class Menus_MenuManager_MpOptionsUpdate
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                //    if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(MenuManager), "MaybeReverseOption"))
                if (state == 0 && code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 100)
                {
                    state = 1;
                    // A simple Postfix breaks the left arrow functionality, have to transpile after MaybeReverseOption
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_MpOptionsUpdate), "Update")) { labels = code.labels };
                    code.labels = null;
                }
                yield return code;
            }
        }

        static void Update()
        {
            if (UIManager.PushedSelect(100) || (MenuManager.option_dir && UIManager.PushedDir()) || UIManager.SliderMouseDown())
            {
                MenuManager.MaybeReverseOption();
                switch (UIManager.m_menu_selection)
                {
                    case 6:
                        MenuManager.ChangeMenuState(Menus.msLagCompensation, false);
                        UIManager.DestroyAll(false);
                        MenuManager.PlaySelectSound(1f);
                        break;
                    case 8:
                        Menus.mms_sticky_death_summary = !Menus.mms_sticky_death_summary;
                        MPDeathReview.stickyDeathReview = Menus.mms_sticky_death_summary;
                        MenuManager.PlaySelectSound(1f);
                        break;
                    case 9:
                        Menus.mms_damageeffect_drunk_blur_mult = (int)(UIElement.SliderPos * 100f);
                        if (Input.GetMouseButtonDown(0))
                            MenuManager.PlayCycleSound(1f, (float)((double)UIElement.SliderPos * 5.0 - 3.0));
                        break;
                    case 10:
                        Menus.mms_damageeffect_alpha_mult = (int)(UIElement.SliderPos * 100f);
                        if (Input.GetMouseButtonDown(0))
                            MenuManager.PlayCycleSound(1f, (float)((double)UIElement.SliderPos * 5.0 - 3.0));
                        break;
                    case 11:
                        MenuManager.ChangeMenuState(Menus.msTeamColors, false);
                        UIManager.DestroyAll(false);
                        MenuManager.PlaySelectSound(1f);
                        break;
                    case 12:
                        Menus.mms_reduced_ship_explosions = !Menus.mms_reduced_ship_explosions;
                        MenuManager.PlaySelectSound(1f);
                        break;
                    default:
                        break;
                }
                MenuManager.UnReverseOption();
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "Update")]
    class Menus_MenuManager_Update
    {

        private static void Postfix(ref float ___m_menu_state_timer)
        {
            if (MenuManager.m_menu_state == Menus.msLagCompensation)
                LagCompensationUpdate(ref ___m_menu_state_timer);

            if (MenuManager.m_menu_state == Menus.msTeamColors)
                TeamColorsUpdate(ref ___m_menu_state_timer);
        }

        private static void LagCompensationUpdate(ref float m_menu_state_timer)
        {
            UIManager.MouseSelectUpdate();
            switch (MenuManager.m_menu_sub_state)
            {
                case MenuSubState.INIT:
                    if (m_menu_state_timer > 0.25f)
                    {
                        UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, Menus.uiLagCompensation);
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
                            switch (UIManager.m_menu_selection)
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
                                case 100:
                                    MenuManager.PlaySelectSound(1f);
                                    m_menu_state_timer = 0f;
                                    UIManager.DestroyAll(false);
                                    MenuManager.m_menu_state = 0;
                                    MenuManager.m_menu_micro_state = 0;
                                    MenuManager.m_menu_sub_state = MenuSubState.BACK;
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

        private static void TeamColorsUpdate(ref float m_menu_state_timer)
        {
            UIManager.MouseSelectUpdate();
            switch (MenuManager.m_menu_sub_state)
            {
                case MenuSubState.INIT:
                    if (m_menu_state_timer > 0.25f)
                    {
                        UIManager.CreateUIElement(UIManager.SCREEN_CENTER, 7000, Menus.uiTeamColors);
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
                            switch (UIManager.m_menu_selection)
                            {
                                case 1:
                                    Menus.mms_team_color_default = !Menus.mms_team_color_default;
                                    MenuManager.PlaySelectSound(1f);
                                    ProcessColorSelections();
                                    break;
                                case 2:
                                    Menus.mms_team_color_self = (Menus.mms_team_color_self + 9 + UIManager.m_select_dir) % 9;
                                    if (Menus.mms_team_color_self == Menus.mms_team_color_enemy)
                                        Menus.mms_team_color_self = (Menus.mms_team_color_self + 9 + UIManager.m_select_dir) % 9;
                                    MenuManager.PlaySelectSound(1f);
                                    ProcessColorSelections();
                                    break;
                                case 3:
                                    Menus.mms_team_color_enemy = (Menus.mms_team_color_enemy + 9 + UIManager.m_select_dir) % 9;
                                    if (Menus.mms_team_color_enemy == Menus.mms_team_color_self)
                                        Menus.mms_team_color_enemy = (Menus.mms_team_color_enemy + 9 + UIManager.m_select_dir) % 9;
                                    MenuManager.PlaySelectSound(1f);
                                    ProcessColorSelections();
                                    break;
                                case 100:
                                    MenuManager.PlaySelectSound(1f);
                                    m_menu_state_timer = 0f;
                                    UIManager.DestroyAll(false);
                                    MenuManager.m_menu_state = 0;
                                    MenuManager.m_menu_micro_state = 0;
                                    MenuManager.m_menu_sub_state = MenuSubState.BACK;
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

        static void ProcessColorSelections()
        {
            if (GameplayManager.IsMultiplayerActive)
            {
                // Update ship colors
                foreach (var ps in UnityEngine.Object.FindObjectsOfType<PlayerShip>())
                {
                    ps.UpdateShipColors(ps.c_player.m_mp_team, -1, -1, -1);
                    ps.UpdateRimColor(true);
                }
                // Update CTF flag/carrier colors
                if (CTF.IsActive)
                {
                    for (int i = 0; i < CTF.FlagObjs.Count; i++)
                    {
                        CTF.UpdateFlagColor(CTF.FlagObjs[i], i);
                    }
                    foreach (var player in Overload.NetworkManager.m_Players)
                    {
                        CTF.UpdateShipEffects(player);
                    }
                }
            }
            UIManager.InitMpNames();
        }
    }

    [HarmonyPatch(typeof(UIElement), "Draw")]
    class Menus_UIElement_Draw
    {
        static void Postfix(UIElement __instance)
        {
            if (__instance.m_type == Menus.uiLagCompensation && __instance.m_alpha > 0f)
                DrawLagCompensationWindow(__instance);

            if (__instance.m_type == Menus.uiTeamColors && __instance.m_alpha > 0f)
                DrawTeamColorsWindow(__instance);
        }

        static void DrawTeamColorsWindow(UIElement uie)
        {
            UIManager.ui_bg_dark = true;
            uie.DrawMenuBG();
            Vector2 position = uie.m_position;
            position.y = UIManager.UI_TOP + 64f;
            uie.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 30f), Loc.LS("TEAM COLOR SETTINGS"), 265f);
            position.y += 20f;
            uie.DrawMenuSeparator(position);
            position.y += 40f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("TEAM COLORS"), position, 1, Menus.GetMMSTeamColorDefault(), "DISPLAY TEAM COLORS IN DEFAULT ORANGE/BLUE OR CUSTOM", 1.5f, false);
            position.y += 64f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("MY TEAM"), position, 2, Menus.GetMMSTeamColorSelf(), "", 1.5f, Menus.mms_team_color_default);
            position.y += 64f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("ENEMY TEAM"), position, 3, Menus.GetMMSTeamColorEnemy(), "", 1.5f, Menus.mms_team_color_default);
            position.y += 64f;
            position.y = UIManager.UI_BOTTOM - 120f;
            uie.DrawMenuSeparator(position);
            position.y += 5f;
            DrawMenuToolTipMultiline(uie, position, 15f);
            position.y = UIManager.UI_BOTTOM - 30f;
            uie.SelectAndDrawItem(Loc.LS("BACK"), position, 100, fade: false);
        }

        static void DrawLagCompensationWindow(UIElement uie)
        {
            UIManager.ui_bg_dark = true;
            uie.DrawMenuBG();
            Vector2 position = uie.m_position;
            position.y = UIManager.UI_TOP + 64f;
            uie.DrawHeaderMedium(Vector2.up * (UIManager.UI_TOP + 30f), Loc.LS("LAG COMPENSATION SETTINGS"), 265f);
            position.y += 20f;
            uie.DrawMenuSeparator(position);
            position.y += 40f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("LAG COMPENSATION"), position, 1, Menus.GetMMSLagCompensation(), "ENABLE LAG COMPENSATION FOR MULTIPLAYER GAMES", 1.5f, false);
            position.y += 62f;
            uie.SelectAndDrawStringOptionItem(Loc.LS("USE ADVANCED SETTINGS"), position, 2, Menus.GetMMSLagCompensationAdvanced(), "SHOW ADVANCED SETTINGS TO FURTHER FINE TUNE YOUR LAG COMPENSATION", 1.5f, false);
            position.y += 62f;
            uie.DrawTextLineSmall("WARNING: ONCE ON, TURNING OFF ADVANCED SETTINGS", position);
            position.y += 28f;
            uie.DrawTextLineSmall("WILL RESET YOUR LAG COMPENSATION SETTINGS TO DEFAULT.", position);
            position.y += 62f;
            if (!Menus.mms_lag_compensation_advanced)
            {
                SelectAndDrawSliderItem(uie, Loc.LS("MAX PING TO COMPENSATE"), position, 3, Menus.mms_ship_lag_compensation_max, 250, "LIMIT LAG COMPENSATION IF YOUR PING EXCEEDS THIS AMOUNT. HAS NO EFFECT WHEN YOUR PING IS LOWER THAN THIS AMOUNT", Menus.mms_lag_compensation == 0);
                position.y += 62f;
                uie.SelectAndDrawStringOptionItem(Loc.LS("LAG COMPENSATION STRENGTH"), position, 4, Menus.GetMMSLagCompensationStrength(), "SCALES THE STRENGTH OF LAG COMPENSATION RELATIVE TO YOUR PING", fade: Menus.mms_lag_compensation == 0);
                position.y += 62f;
                uie.SelectAndDrawStringOptionItem(Loc.LS("USE INTERPOLATION"), position, 5, Menus.GetMMSLagCompensationUseInterpolation(), "HOW STRONGLY TO INTERPOLATE SHIP POSITIONS AT THE COST OF ADDED LAG." + Environment.NewLine + "A STRONGER VALUE WILL BETTER SHOW SHIP POSITIONS WITHOUT GUESSING, BUT REQUIRE YOU TO LEAD SHIPS MORE", fade: Menus.mms_lag_compensation == 1 || Menus.mms_lag_compensation == 3);
                position.y += 62f;
            }
            else
            {
                SelectAndDrawSliderItem(uie, Loc.LS("MAX PING TO COMPENSATE FOR WEAPONS"), position, 6, Menus.mms_weapon_lag_compensation_max, 250, "LIMIT WEAPON LAG COMPENSATION IF YOUR PING EXCEEDS THIS AMOUNT. HAS NO EFFECT WHEN YOUR PING IS LOWER THAN THIS AMOUNT." + Environment.NewLine + "AT HIGHER PING, A LOWER SETTING LIMITS HOW FAR FROM THE FIRING SHIP WEAPONS WILL BE DRAWN, AT THE COST OF HAVING TO LEAD YOUR DODGES MORE", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 1);
                position.y += 62f;
                SelectAndDrawSliderItem(uie, Loc.LS("MAX PING TO COMPENSATE FOR SHIPS"), position, 7, Menus.mms_ship_lag_compensation_max, 250, "LIMIT SHIP LAG COMPENSATION IF YOUR PING EXCEEDS THIS AMOUNT. HAS NO EFFECT WHEN YOUR PING IS LOWER THAN THIS AMOUNT." + Environment.NewLine + "AT HIGHER PING, A LOWER SETTING LIMITS HOW FAR INTO THE FUTURE SHIP POSITIONS WILL BE GUESSED, AT THE COST OF HAVING TO LEAD YOUR SHOTS MORE", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 2);
                position.y += 62f;
                SelectAndDrawSliderItem(uie, Loc.LS("WEAPON LAG COMPENSATION SCALE"), position, 8, Menus.mms_weapon_lag_compensation_scale, 100, "THE SCALE AT WHICH WEAPON LAG IS COMPENSATED MEASURED AS A PERCENTAGE OF THE AMOUNT OF PING YOU ARE COMPENSATING FOR." + Environment.NewLine + "A SCALE OF 100% WILL MAKE YOUR DODGING CLOSELY MATCH THE SERVER WHEN YOUR PING IS LESS THAN YOUR MAX PING TO COMPENSATE FOR WEAPONS", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 1);
                position.y += 62f;
                SelectAndDrawSliderItem(uie, Loc.LS("SHIP LAG COMPENSATION SCALE"), position, 9, Menus.mms_ship_lag_compensation_scale, 100, "THE SCALE AT WHICH SHIP LAG IS COMPENSATED MEASURED AS A PERCENTAGE OF THE AMOUNT OF PING YOU ARE COMPENSATING FOR." + Environment.NewLine + "A SCALE OF 100% WILL MAKE YOUR SHOTS THAT HIT SHIPS CLOSELY MATCH THE SERVER WHEN YOUR PING IS LESS THAN YOUR MAX PING TO COMPENSATE FOR SHIPS", Menus.mms_lag_compensation == 0 || Menus.mms_lag_compensation == 2);
                position.y += 62f;
                SelectAndDrawSliderItem(uie, Loc.LS("SHIP LAG ADDED"), position, 10, Menus.mms_lag_compensation_ship_added_lag, 50, "ADDS A SET AMOUNT OF LAG TO THE END OF THE SHIP LAG COMPENSATION CALCULATIONS. USEFUL WHEN SHIP LAG COMPENSATION IS TURNED OFF." + Environment.NewLine + "A HIGHER SETTING WILL BETTER SHOW SHIP POSITIONS WITHOUT GUESSING, BUT REQUIRE YOU TO LEAD SHIPS MORE");
                position.y += 62f;
            }
            position.y = UIManager.UI_BOTTOM - 120f;
            uie.DrawMenuSeparator(position);
            position.y += 5f;
            DrawMenuToolTipMultiline(uie, position, 15f);
            position.y = UIManager.UI_BOTTOM - 30f;
            uie.SelectAndDrawItem(Loc.LS("BACK"), position, 100, fade: false);
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

        public static void DrawMenuToolTipMultiline(UIElement uie, Vector2 pos, float offset = 15f)
        {
            if (!UIElement.ToolTipActive)
            {
                return;
            }
            string str = (!(UIElement.ToolTipTitle != string.Empty)) ? string.Empty : ("[" + UIElement.ToolTipTitle + "] - ");
            pos.y += offset;
            UIManager.DrawQuadUI(pos, 700f, 10f, UIManager.m_col_ub2, 0.7f * uie.m_alpha, 20);
            string[] strs = UIElement.ToolTipDescription.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            for (int i = 0; i < strs.Length; i++)
            {
                uie.DrawStringSmall((i == 0 ? str : "") + strs[i], pos - (Vector2.down * i * 20f), 0.5f, StringOffset.CENTER, UIManager.m_col_ub0, 1f, 1280f);
            }

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
        private static void DrawSoundReload(UIElement uie, ref Vector2 position)
        {
            position.y += 62f;
            uie.SelectAndDrawItem("REINITIALIZE AUDIO DEVICE", position, 5, false);
        }

        // First SelectAndDrawStringOptionItem() after "SPEAKER MODE" string, draw our menu
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "SPEAKER MODE")
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(UIElement), "SelectAndDrawStringOptionItem"))
                {
                    state = 2;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_UIElement_DrawSoundMenu), "DrawSoundReload"));
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
        private static void HandleSoundReload(int menu_selection)
        {
            if (menu_selection == 5)
            {
                AudioSettings.Reset(AudioSettings.GetConfiguration());
                MenuManager.PlaySelectSound(1f);
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
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Menus_MenuManager_SoundOptionsUpdate), "HandleSoundReload"));
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
}
