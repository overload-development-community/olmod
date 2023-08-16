using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    [HarmonyPatch(typeof(NetworkMatch), "SetDefaultMatchSettings")]
    class MPSetupDefault
    {
        public static void Postfix()
        {
            MPDownloadLevel.Reset();
            MPTeams.NetworkMatchTeamCount = 2;
            MPJoinInProgress.NetworkMatchEnabled = false;
            RearView.MPNetworkMatchEnabled = false;
            MPSuddenDeath.SuddenDeathMatchEnabled = false;
            MPClassic.matchEnabled = false;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ApplyPrivateMatchSettings")]
    class MPSetupApplyPMD
    {
        public static void ApplyMatchOLModData()
        {
            Debug.Log("Apply PMD name " + String.Join(",", NetworkMatch.m_name.Select(x => ((int)x).ToString()).ToArray()));
            var i = NetworkMatch.m_name.IndexOf('\0');
            if (i == -1)
            {
                MPSetupDefault.Postfix();
            }
            else
            {
                MPTeams.NetworkMatchTeamCount = (NetworkMatch.m_name[i + 1] & 7) + 2;
                MPJoinInProgress.NetworkMatchEnabled = (NetworkMatch.m_name[i + 1] & 8) != 0;
                RearView.MPNetworkMatchEnabled = (NetworkMatch.m_name[i + 1] & 16) != 0;
                MPSuddenDeath.SuddenDeathMatchEnabled = (NetworkMatch.m_name[i + 1] & 32) != 0;
            }
        }

        private static void Postfix(ref bool __result, PrivateMatchDataMessage pmd, ref int ___m_num_players_to_start_match)
        {
            ApplyMatchOLModData();
            if (___m_num_players_to_start_match == 2) // always allow start with 1
                ___m_num_players_to_start_match = 1;
            if (!__result && !Config.NoDownload && !string.IsNullOrEmpty(pmd.m_addon_level_name_hash)) // unknown level?
            {
                MPDownloadLevel.StartGetLevel(pmd.m_addon_level_name_hash);
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    class MPSetupAcceptedToLobby
    {
        static void Postfix()
        {
            MPSetupApplyPMD.ApplyMatchOLModData();
        }
    }

    [HarmonyPatch(typeof(MenuManager), "BuildPrivateMatchData")]
    class MPSetupBuildPMD
    {
        static void Postfix(PrivateMatchDataMessage __result)
        {
            Debug.Log("Build PMD name jipsingle " + MPJoinInProgress.SingleMatchEnable);
            if ((MPTeams.MenuManagerTeamCount > 2 || MPJoinInProgress.MenuManagerEnabled || MPJoinInProgress.SingleMatchEnable || RearView.MPMenuManagerEnabled ||
                MPSuddenDeath.SuddenDeathMenuEnabled) &&
                MenuManager.m_mp_lan_match)
            {
                __result.m_name += new string(new char[] { '\0', (char)(
                    ((Math.Max(2, MPTeams.MenuManagerTeamCount) - 2) & 7) |
                    (MPJoinInProgress.MenuManagerEnabled || MPJoinInProgress.SingleMatchEnable ? 8 : 0) |
                    (RearView.MPMenuManagerEnabled ? 16 : 0) |
                    (MPSuddenDeath.SuddenDeathMenuEnabled ? 32 : 0))});
            }
            Debug.Log("Build PMD name " + String.Join(",", __result.m_name.Select(x => ((int)x).ToString()).ToArray()));
            if (MPJoinInProgress.MenuManagerEnabled || MPJoinInProgress.SingleMatchEnable ||
                (MenuManager.m_mp_lan_match && MPInternet.Enabled))
                __result.m_num_players_to_start = 1;
            if ((int)__result.m_match_mode > (int)ExtMatchMode.CTF) // newer matchmodes are sent with ModPrivateData
                __result.m_match_mode = NetworkMatch.IsTeamMode(__result.m_match_mode) ? MatchMode.TEAM_ANARCHY : MatchMode.ANARCHY;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "InitMpPrivateMatch")]
    class MPSetupMenuInit
    {
        public static void Postfix()
        {
            MPTeams.MenuManagerTeamCount = 2;
            MPJoinInProgress.MenuManagerEnabled = false;
            RearView.MPMenuManagerEnabled = false;
            MPSuddenDeath.SuddenDeathMenuEnabled = false;
            ExtMenuManager.mms_ext_lap_limit = 0;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "SetPreferencesDefaults")]
    class MPSetupMenuDefault
    {
        static void Postfix()
        {
            MPSetupMenuInit.Postfix();
            Console.KeyEnabled = false;
            Console.CustomUIColor = 0;
        }
    }

    static class ModPrefs
    {
        private static Hashtable m_prefs_hashtable = new Hashtable();
        private static string m_serialized_data = string.Empty;

        private static object oldPrefs, oldData;

        private static FieldInfo _GamePreferences_m_prefs_hashtable_Field = typeof(GamePreferences).GetField("m_prefs_hashtable", BindingFlags.NonPublic | BindingFlags.Static);
        private static FieldInfo _GamePreferences_m_serialized_data_Field = typeof(GamePreferences).GetField("m_serialized_data", BindingFlags.NonPublic | BindingFlags.Static);
        private static void SwapPre()
        {
            oldPrefs = _GamePreferences_m_prefs_hashtable_Field.GetValue(null);
            oldData = _GamePreferences_m_serialized_data_Field.GetValue(null);
            _GamePreferences_m_prefs_hashtable_Field.SetValue(null, m_prefs_hashtable);
            _GamePreferences_m_serialized_data_Field.SetValue(null, m_serialized_data);
        }

        private static void SwapPost()
        {
            m_prefs_hashtable = (Hashtable)_GamePreferences_m_prefs_hashtable_Field.GetValue(null);
            m_serialized_data = (string)_GamePreferences_m_serialized_data_Field.GetValue(null);
            _GamePreferences_m_prefs_hashtable_Field.SetValue(null, oldPrefs);
            _GamePreferences_m_serialized_data_Field.SetValue(null, oldData);
            oldPrefs = null;
            oldData = null;
        }

        public static bool Load(string filename)
        {
            SwapPre();
            GamePreferences.Load(filename);
            SwapPost();
            return m_serialized_data != null;
        }

        public static void Flush(string filename)
        {
            SwapPre();
            GamePreferences.Flush(filename);
            SwapPost();
        }

        public static void DeleteAll()
        {
            m_prefs_hashtable.Clear();
        }

        public static int GetInt(string key, int defaultValue)
        {
            try {
                if (m_prefs_hashtable.ContainsKey(key)) {
                    return (int)m_prefs_hashtable[key];
                }
            } catch (Exception) {
                Debug.Log($"MPSetup: Could not convert key {key} value {m_prefs_hashtable[key]} to an int, resetting to default {defaultValue}.");
            }
            m_prefs_hashtable.Add(key, defaultValue);
            return defaultValue;
        }

        public static bool GetBool(string key, bool defaultValue)
        {
            try {
                if (m_prefs_hashtable.ContainsKey(key)) {
                    return (bool)m_prefs_hashtable[key];
                }
            } catch (Exception) {
                Debug.Log($"MPSetup: Could not convert key {key} value {m_prefs_hashtable[key]} to a bool, resetting to default {defaultValue}.");
            }
            m_prefs_hashtable.Add(key, defaultValue);
            return defaultValue;
        }
        
        public static void SetInt(string key, int value)
        {
            m_prefs_hashtable[key] = value;
        }

        public static void SetBool(string key, bool value)
        {
            m_prefs_hashtable[key] = value;
        }

        public static string GetString(string key, string defaultValue)
        {
            if (m_prefs_hashtable.ContainsKey(key))
            {
                return (string)m_prefs_hashtable[key];
            }
            m_prefs_hashtable.Add(key, defaultValue);
            return defaultValue;
        }

        public static void SetString(string key, string value)
        {
            m_prefs_hashtable[key] = value;
        }
    }

    [HarmonyPatch(typeof(MenuManager), "LoadPreferences")]
    class MPSetupLoad
    {
        static void Postfix(string filename)
        {
            if (ModPrefs.Load(filename + "mod"))
            {
                MPTeams.MenuManagerTeamCount = ModPrefs.GetInt("MP_PM_TEAM_COUNT", MPTeams.MenuManagerTeamCount);
                MPJoinInProgress.MenuManagerEnabled = ModPrefs.GetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
                RearView.MPMenuManagerEnabled = ModPrefs.GetBool("MP_PM_REARVIEW", RearView.MPMenuManagerEnabled);
                RearView.MenuManagerEnabled = ModPrefs.GetBool("O_PM_REARVIEW", RearView.MenuManagerEnabled);
                MPSuddenDeath.SuddenDeathMenuEnabled = ModPrefs.GetBool("MP_PM_SUDDENDEATH", MPSuddenDeath.SuddenDeathMenuEnabled);
                ExtMenuManager.mms_ext_lap_limit = ModPrefs.GetInt("MP_PM_LAP_LIMIT", ExtMenuManager.mms_ext_lap_limit);
                Console.KeyEnabled = ModPrefs.GetBool("O_CONSOLE_KEY", Console.KeyEnabled);
                Console.CustomUIColor = ModPrefs.GetInt("O_CUSTOM_UI_COLOR", Console.CustomUIColor);
                Menus.mms_damage_numbers = ModPrefs.GetBool("MP_DAMAGE_NUMBERS", Menus.mms_damage_numbers);
                Menus.mms_client_damage_numbers = ModPrefs.GetBool("MP_CLIENT_DAMAGE_NUMBERS", Menus.mms_client_damage_numbers);
                MPThunderboltPassthrough.isAllowed = ModPrefs.GetBool("MP_THUNDERBOLT_PASSTHROUGH", MPThunderboltPassthrough.isAllowed);
                Menus.mms_always_cloaked = ModPrefs.GetBool("MP_ALWAYS_CLOAKED", Menus.mms_always_cloaked);
                Menus.mms_classic_spawns = ModPrefs.GetBool("MP_CLASSIC_SPAWNS", Menus.mms_classic_spawns);
                Menus.mms_assist_scoring = ModPrefs.GetBool("MP_ASSIST_SCORING", Menus.mms_assist_scoring);
                Menus.mms_allow_smash = ModPrefs.GetBool("MP_ALLOW_SMASH", Menus.mms_allow_smash);

                JoystickRotationFix.alt_turn_ramp_mode = ModPrefs.GetBool("SCALE_UP_ROTATION", JoystickRotationFix.alt_turn_ramp_mode);
                MPColoredPlayerNames.isActive = ModPrefs.GetBool("MP_COLORED_NAMES", MPColoredPlayerNames.isActive);
                DisableProfanityFilter.profanity_filter = ModPrefs.GetBool("MP_PROFANITY_FILTER", DisableProfanityFilter.profanity_filter);
                Menus.mms_scale_respawn_time = ModPrefs.GetBool("MP_PM_SCALE_RESPAWN_TIME", Menus.mms_scale_respawn_time);
                if (Core.GameMod.HasInternetMatch())
                    MPInternet.MenuIPAddress = ModPrefs.GetString("MP_PM_IP_ADDRESS", MPInternet.MenuIPAddress);
                Menus.mms_lag_compensation = ModPrefs.GetInt("MP_PM_LAG_COMPENSATION", Menus.mms_lag_compensation);
                Menus.mms_lag_compensation_advanced = ModPrefs.GetBool("MP_PM_LAG_COMPENSATION_ADVANCED", Menus.mms_lag_compensation_advanced);
                Menus.mms_ship_lag_compensation_max = ModPrefs.GetInt("MP_PM_SHIP_LAG_COMPENSATION", Menus.mms_ship_lag_compensation_max);
                Menus.mms_weapon_lag_compensation_max = ModPrefs.GetInt("MP_PM_WEAPON_LAG_COMPENSATION", Menus.mms_weapon_lag_compensation_max);
                Menus.mms_lag_compensation_strength = ModPrefs.GetInt("MP_PM_LAG_COMPENSATION_STRENGTH", Menus.mms_lag_compensation_strength);
                Menus.mms_lag_compensation_use_interpolation = ModPrefs.GetInt("MP_PM_LAG_COMPENSATION_USE_INTERPOLATION", Menus.mms_lag_compensation_use_interpolation);
                Menus.mms_lag_compensation_ship_added_lag = ModPrefs.GetInt("MP_PM_LAG_COMPENSATION_SHIP_ADDED_LAG", Menus.mms_lag_compensation_ship_added_lag);
                Menus.mms_ship_lag_compensation_scale = ModPrefs.GetInt("MP_PM_SHIP_LAG_COMPENSATION_SCALE", Menus.mms_ship_lag_compensation_scale);
                Menus.mms_weapon_lag_compensation_scale = ModPrefs.GetInt("MP_PM_WEAPON_LAG_COMPENSATION_SCALE", Menus.mms_weapon_lag_compensation_scale);
                Menus.mms_lag_compensation_collision_limit = ModPrefs.GetInt("MP_PM_SHIP_LAG_COMPENSATION_COLLISION_LIMIT", Menus.mms_lag_compensation_collision_limit);
                Menus.mms_sticky_death_summary = ModPrefs.GetBool("MP_PM_STICKY_DEATH_SUMMARY", Menus.mms_sticky_death_summary);
                MPDeathReview.stickyDeathReview = Menus.mms_sticky_death_summary;
                Menus.mms_reduced_ship_explosions = ModPrefs.GetBool("MP_PM_REDUCED_SHIP_EXPLOSIONS", Menus.mms_reduced_ship_explosions);
                Menus.mms_damageeffect_alpha_mult = ModPrefs.GetInt("MP_PM_DAMAGEEFFECT_ALPHA_MULT", Menus.mms_damageeffect_alpha_mult);
                Menus.mms_damageeffect_drunk_blur_mult = ModPrefs.GetInt("MP_PM_DAMAGEEFFECT_DRUNK_BLUR_MULT", Menus.mms_damageeffect_drunk_blur_mult);
                Menus.mms_match_time_limit = ModPrefs.GetInt("MP_PM_MATCH_TIME_LIMIT", Menus.mms_match_time_limit);
                Menus.mms_team_color_default = ModPrefs.GetBool("MP_PM_TEAM_COLOR_DEFAULT", Menus.mms_team_color_default);
                Menus.mms_team_color_self = ModPrefs.GetInt("MP_PM_TEAM_COLOR_SELF", Menus.mms_team_color_self);
                Menus.mms_team_color_enemy = ModPrefs.GetInt("MP_PM_TEAM_COLOR_ENEMY", Menus.mms_team_color_enemy);
                Menus.mms_team_health = ModPrefs.GetBool("MP_PM_TEAM_HEALTH", Menus.mms_team_health);
                HUDVelocity.MenuManagerEnabled = ModPrefs.GetBool("MP_PM_SHOWHUDVELOCITY", HUDVelocity.MenuManagerEnabled);
                Menus.mms_show_framerate = ModPrefs.GetBool("MP_PM_SHOWFRAMERATE", Menus.mms_show_framerate);
                Menus.mms_audio_occlusion_strength = ModPrefs.GetInt("MP_PM_AUDIO_OCCLUSION_STRENGTH", Menus.mms_audio_occlusion_strength);
                Menus.mms_directional_warnings = ModPrefs.GetBool("MP_PM_DIRECTIONAL_WARNINGS", Menus.mms_directional_warnings);
                Menus.mms_loadout_hotkeys = ModPrefs.GetInt("MP_PM_LOADOUT_HOTKEYS2", Menus.mms_loadout_hotkeys);
                Menus.mms_creeper_colors = ModPrefs.GetBool("MP_CREEPER_COLORS", Menus.mms_creeper_colors);

                MPLoadouts.Loadouts[0].weapons[0] = (WeaponType)ModPrefs.GetInt("MP_PM_LOADOUT_BOMBER1_W1", (int)MPLoadouts.Loadouts[0].weapons[0]);
                MPLoadouts.Loadouts[0].missiles[0] = (MissileType)ModPrefs.GetInt("MP_PM_LOADOUT_BOMBER1_M1", (int)MPLoadouts.Loadouts[0].missiles[0]);
                MPLoadouts.Loadouts[0].missiles[1] = (MissileType)ModPrefs.GetInt("MP_PM_LOADOUT_BOMBER1_M2", (int)MPLoadouts.Loadouts[0].missiles[1]);

                MPLoadouts.Loadouts[1].weapons[0] = (WeaponType)ModPrefs.GetInt("MP_PM_LOADOUT_GUNNER1_W1", (int)MPLoadouts.Loadouts[1].weapons[0]);
                MPLoadouts.Loadouts[1].weapons[1] = (WeaponType)ModPrefs.GetInt("MP_PM_LOADOUT_GUNNER1_W2", (int)MPLoadouts.Loadouts[1].weapons[1]);
                MPLoadouts.Loadouts[1].missiles[0] = (MissileType)ModPrefs.GetInt("MP_PM_LOADOUT_GUNNER1_M1", (int)MPLoadouts.Loadouts[1].missiles[0]);

                MPLoadouts.Loadouts[2].weapons[0] = (WeaponType)ModPrefs.GetInt("MP_PM_LOADOUT_BOMBER2_W1", (int)MPLoadouts.Loadouts[2].weapons[0]);
                MPLoadouts.Loadouts[2].missiles[0] = (MissileType)ModPrefs.GetInt("MP_PM_LOADOUT_BOMBER2_M1", (int)MPLoadouts.Loadouts[2].missiles[0]);
                MPLoadouts.Loadouts[2].missiles[1] = (MissileType)ModPrefs.GetInt("MP_PM_LOADOUT_BOMBER2_M2", (int)MPLoadouts.Loadouts[2].missiles[1]);

                MPLoadouts.Loadouts[3].weapons[0] = (WeaponType)ModPrefs.GetInt("MP_PM_LOADOUT_GUNNER2_W1", (int)MPLoadouts.Loadouts[1].weapons[0]);
                MPLoadouts.Loadouts[3].weapons[1] = (WeaponType)ModPrefs.GetInt("MP_PM_LOADOUT_GUNNER2_W2", (int)MPLoadouts.Loadouts[1].weapons[1]);
                MPLoadouts.Loadouts[3].missiles[0] = (MissileType)ModPrefs.GetInt("MP_PM_LOADOUT_GUNNER2_M1", (int)MPLoadouts.Loadouts[1].missiles[0]);

                MPAudioTaunts.AClient.active = ModPrefs.GetBool("MP_AUDIOTAUNTS_ACTIVE", true);
                MPAudioTaunts.AClient.audio_taunt_volume = ModPrefs.GetInt("MP_AUDIOTAUNT_VOLUME", 50);
                MPAudioTaunts.AClient.display_audio_spectrum = ModPrefs.GetBool("MP_AUDIOTAUNT_SHOW_FREQUENCYBAND", true);

                Menus.mms_collision_mesh = ModPrefs.GetInt("MP_COLLIDER_MESH", 0);

                //MPShips.allowed = ModPrefs.GetInt("MP_SHIPS_ALLOWED", 1);
                Menus.mms_ships_allowed = ModPrefs.GetInt("MP_SHIPS_ALLOWED", 1);
                MPShips.selected_idx = ModPrefs.GetInt("MP_SHIP_TYPE", 0);

                FramerateLimiter.target_framerate = ModPrefs.GetInt("TARGET_FRAMERATE", 0);
            }
            else // for compatibility with old olmod, no need to add new settings
            {
                MPTeams.MenuManagerTeamCount = MenuManager.LocalGetInt("MP_PM_TEAM_COUNT", MPTeams.MenuManagerTeamCount);
                MPJoinInProgress.MenuManagerEnabled = MenuManager.LocalGetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
                RearView.MPMenuManagerEnabled = MenuManager.LocalGetBool("MP_PM_REARVIEW", RearView.MPMenuManagerEnabled);
                Console.KeyEnabled = MenuManager.LocalGetBool("O_CONSOLE_KEY", Console.KeyEnabled);
                Console.CustomUIColor = MenuManager.LocalGetInt("O_CUSTOM_UI_COLOR", Console.CustomUIColor);
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "SavePreferences")]
    class MPSetupSave
    {
        private static int lastXP;

        public static void Store()
        {
            if (MenuManager.LocalGetInt("PS_XP2", 0) == 0 && lastXP > 0)
                MenuManager.LocalSetInt("PS_XP2", lastXP);
        }

        private static void Prefix(string filename)
        {
            lastXP = MenuManager.LocalGetInt("PS_XP2", 0);

            ModPrefs.DeleteAll();
            ModPrefs.SetInt("MP_PM_TEAM_COUNT", MPTeams.MenuManagerTeamCount);
            ModPrefs.SetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
            ModPrefs.SetBool("MP_PM_REARVIEW", RearView.MPMenuManagerEnabled);
            ModPrefs.SetBool("O_PM_REARVIEW", RearView.MenuManagerEnabled);
            ModPrefs.SetBool("MP_PM_SUDDENDEATH", MPSuddenDeath.SuddenDeathMenuEnabled);
            ModPrefs.SetInt("MP_PM_LAP_LIMIT", ExtMenuManager.mms_ext_lap_limit);
            ModPrefs.SetBool("O_CONSOLE_KEY", Console.KeyEnabled);
            ModPrefs.SetInt("O_CUSTOM_UI_COLOR", Console.CustomUIColor);
            ModPrefs.SetBool("SCALE_UP_ROTATION", JoystickRotationFix.alt_turn_ramp_mode);
            ModPrefs.SetBool("MP_COLORED_NAMES", MPColoredPlayerNames.isActive);
            ModPrefs.SetBool("MP_PROFANITY_FILTER", DisableProfanityFilter.profanity_filter);
            ModPrefs.SetString("MP_PM_IP_ADDRESS", MPInternet.MenuIPAddress);
            ModPrefs.SetBool("MP_PM_SCALE_RESPAWN_TIME", Menus.mms_scale_respawn_time);
            ModPrefs.SetInt("MP_PM_LAG_COMPENSATION", Menus.mms_lag_compensation);
            ModPrefs.SetBool("MP_PM_LAG_COMPENSATION_ADVANCED", Menus.mms_lag_compensation_advanced);
            ModPrefs.SetInt("MP_PM_SHIP_LAG_COMPENSATION", Menus.mms_ship_lag_compensation_max);
            ModPrefs.SetInt("MP_PM_WEAPON_LAG_COMPENSATION", Menus.mms_weapon_lag_compensation_max);
            ModPrefs.SetInt("MP_PM_LAG_COMPENSATION_STRENGTH", Menus.mms_lag_compensation_strength);
            ModPrefs.SetInt("MP_PM_LAG_COMPENSATION_USE_INTERPOLATION", Menus.mms_lag_compensation_use_interpolation);
            ModPrefs.SetInt("MP_PM_LAG_COMPENSATION_SHIP_ADDED_LAG", Menus.mms_lag_compensation_ship_added_lag);
            ModPrefs.SetInt("MP_PM_WEAPON_LAG_COMPENSATION_SCALE", Menus.mms_weapon_lag_compensation_scale);
            ModPrefs.SetInt("MP_PM_SHIP_LAG_COMPENSATION_SCALE", Menus.mms_ship_lag_compensation_scale);
            ModPrefs.SetInt("MP_PM_SHIP_LAG_COMPENSATION_COLLISION_LIMIT", Menus.mms_lag_compensation_collision_limit);
            ModPrefs.SetBool("MP_PM_STICKY_DEATH_SUMMARY", Menus.mms_sticky_death_summary);
            ModPrefs.SetBool("MP_PM_REDUCED_SHIP_EXPLOSIONS", Menus.mms_reduced_ship_explosions);
            ModPrefs.SetInt("MP_PM_DAMAGEEFFECT_ALPHA_MULT", Menus.mms_damageeffect_alpha_mult);
            ModPrefs.SetInt("MP_PM_DAMAGEEFFECT_DRUNK_BLUR_MULT", Menus.mms_damageeffect_drunk_blur_mult);
            ModPrefs.SetInt("MP_PM_MATCH_TIME_LIMIT", Menus.mms_match_time_limit);
            ModPrefs.SetBool("MP_PM_TEAM_COLOR_DEFAULT", Menus.mms_team_color_default);
            ModPrefs.SetInt("MP_PM_TEAM_COLOR_SELF", Menus.mms_team_color_self);
            ModPrefs.SetInt("MP_PM_TEAM_COLOR_ENEMY", Menus.mms_team_color_enemy);
            ModPrefs.SetBool("MP_PM_TEAM_HEALTH", Menus.mms_team_health);
            ModPrefs.SetBool("MP_PM_SHOWHUDVELOCITY", HUDVelocity.MenuManagerEnabled);
            ModPrefs.SetBool("MP_PM_SHOWFRAMERATE", Menus.mms_show_framerate);
            ModPrefs.SetInt("MP_PM_AUDIO_OCCLUSION_STRENGTH", Menus.mms_audio_occlusion_strength);
            ModPrefs.SetBool("MP_PM_DIRECTIONAL_WARNINGS", Menus.mms_directional_warnings);
            ModPrefs.SetInt("MP_PM_LOADOUT_HOTKEYS2", Menus.mms_loadout_hotkeys);
            ModPrefs.SetInt("MP_PM_LOADOUT_BOMBER1_W1", (int)MPLoadouts.Loadouts[0].weapons[0]);
            ModPrefs.SetInt("MP_PM_LOADOUT_BOMBER1_M1", (int)MPLoadouts.Loadouts[0].missiles[0]);
            ModPrefs.SetInt("MP_PM_LOADOUT_BOMBER1_M2", (int)MPLoadouts.Loadouts[0].missiles[1]);
            ModPrefs.SetInt("MP_PM_LOADOUT_GUNNER1_W1", (int)MPLoadouts.Loadouts[1].weapons[0]);
            ModPrefs.SetInt("MP_PM_LOADOUT_GUNNER1_W2", (int)MPLoadouts.Loadouts[1].weapons[1]);
            ModPrefs.SetInt("MP_PM_LOADOUT_GUNNER1_M1", (int)MPLoadouts.Loadouts[1].missiles[0]);
            ModPrefs.SetInt("MP_PM_LOADOUT_BOMBER2_W1", (int)MPLoadouts.Loadouts[2].weapons[0]);
            ModPrefs.SetInt("MP_PM_LOADOUT_BOMBER2_M1", (int)MPLoadouts.Loadouts[2].missiles[0]);
            ModPrefs.SetInt("MP_PM_LOADOUT_BOMBER2_M2", (int)MPLoadouts.Loadouts[2].missiles[1]);
            ModPrefs.SetInt("MP_PM_LOADOUT_GUNNER2_W1", (int)MPLoadouts.Loadouts[3].weapons[0]);
            ModPrefs.SetInt("MP_PM_LOADOUT_GUNNER2_W2", (int)MPLoadouts.Loadouts[3].weapons[1]);
            ModPrefs.SetInt("MP_PM_LOADOUT_GUNNER2_M1", (int)MPLoadouts.Loadouts[3].missiles[0]);
            ModPrefs.SetBool("MP_DAMAGE_NUMBERS", Menus.mms_damage_numbers);
            ModPrefs.SetBool("MP_CLIENT_DAMAGE_NUMBERS", Menus.mms_client_damage_numbers);
            ModPrefs.SetBool("MP_THUNDERBOLT_PASSTHROUGH", MPThunderboltPassthrough.isAllowed);
            ModPrefs.SetBool("MP_ALWAYS_CLOAKED", Menus.mms_always_cloaked);
            ModPrefs.SetBool("MP_CLASSIC_SPAWNS", Menus.mms_classic_spawns);
            ModPrefs.SetBool("MP_ASSIST_SCORING", Menus.mms_assist_scoring);
            ModPrefs.SetBool("MP_ALLOW_SMASH", Menus.mms_allow_smash);
            ModPrefs.SetBool("MP_CREEPER_COLORS", Menus.mms_creeper_colors);
            ModPrefs.SetBool("MP_AUDIOTAUNTS_ACTIVE", MPAudioTaunts.AClient.active);
            ModPrefs.SetInt("MP_AUDIOTAUNT_VOLUME", MPAudioTaunts.AClient.audio_taunt_volume);
            ModPrefs.SetBool("MP_AUDIOTAUNT_SHOW_FREQUENCYBAND", MPAudioTaunts.AClient.display_audio_spectrum);
            ModPrefs.SetInt("TARGET_FRAMERATE", FramerateLimiter.target_framerate);

            ModPrefs.SetInt("MP_COLLIDER_MESH", Menus.mms_collision_mesh);

            //ModPrefs.SetInt("MP_SHIPS_ALLOWED", MPShips.allowed);
            ModPrefs.SetInt("MP_SHIPS_ALLOWED", Menus.mms_ships_allowed);
            ModPrefs.SetInt("MP_SHIP_TYPE", MPShips.selected_idx);

            ModPrefs.Flush(filename + "mod");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var mpSetupSave_Store_Method = AccessTools.Method(typeof(MPSetupSave), "Store");
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "Flush")
                    yield return new CodeInstruction(OpCodes.Call, mpSetupSave_Store_Method);
                yield return code;
            }
        }
    }
}
