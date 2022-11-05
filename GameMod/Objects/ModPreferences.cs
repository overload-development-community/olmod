using System;
using System.Collections;
using System.Reflection;
using GameMod.Metadata;
using Overload;
using UnityEngine;

namespace GameMod.Objects {
    /// <summary>
    /// A class to handle modded preferences.
    /// </summary>
    [Mod(Mods.ModPreferences)]
    public static class ModPreferences {
        private static Hashtable m_prefs_hashtable = new Hashtable();
        private static string m_serialized_data = string.Empty;

        private static object oldPrefs, oldData;

        private static readonly FieldInfo _GamePreferences_m_prefs_hashtable_Field = typeof(GamePreferences).GetField("m_prefs_hashtable", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo _GamePreferences_m_serialized_data_Field = typeof(GamePreferences).GetField("m_serialized_data", BindingFlags.NonPublic | BindingFlags.Static);
        private static void SwapPre() {
            oldPrefs = _GamePreferences_m_prefs_hashtable_Field.GetValue(null);
            oldData = _GamePreferences_m_serialized_data_Field.GetValue(null);
            _GamePreferences_m_prefs_hashtable_Field.SetValue(null, m_prefs_hashtable);
            _GamePreferences_m_serialized_data_Field.SetValue(null, m_serialized_data);
        }

        private static void SwapPost() {
            m_prefs_hashtable = (Hashtable)_GamePreferences_m_prefs_hashtable_Field.GetValue(null);
            m_serialized_data = (string)_GamePreferences_m_serialized_data_Field.GetValue(null);
            _GamePreferences_m_prefs_hashtable_Field.SetValue(null, oldPrefs);
            _GamePreferences_m_serialized_data_Field.SetValue(null, oldData);
            oldPrefs = null;
            oldData = null;
        }

        public static bool Load(string filename) {
            SwapPre();
            GamePreferences.Load(filename);
            SwapPost();
            return m_serialized_data != null;
        }

        public static void Flush(string filename) {
            SwapPre();
            GamePreferences.Flush(filename);
            SwapPost();
        }

        public static void DeleteAll() {
            m_prefs_hashtable.Clear();
        }

        public static int GetInt(string key, int defaultValue) {
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

        public static bool GetBool(string key, bool defaultValue) {
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

        public static void SetInt(string key, int value) {
            m_prefs_hashtable[key] = value;
        }

        public static void SetBool(string key, bool value) {
            m_prefs_hashtable[key] = value;
        }

        public static string GetString(string key, string defaultValue) {
            if (m_prefs_hashtable.ContainsKey(key)) {
                return (string)m_prefs_hashtable[key];
            }
            m_prefs_hashtable.Add(key, defaultValue);
            return defaultValue;
        }

        public static void SetString(string key, string value) {
            m_prefs_hashtable[key] = value;
        }

        public static void InitSettings() {
            Teams.MenuManagerTeamCount = 2;
            ExtMenuManager.mms_ext_lap_limit = 0;
        }

        public static void SetDefaults() {
            Console.KeyEnabled = false;
            Console.CustomUIColor = 0;
        }

        public static void LoadPreferences() {
            Teams.MenuManagerTeamCount = GetInt("MP_PM_TEAM_COUNT", Teams.MenuManagerTeamCount);
            MPJoinInProgress.MenuManagerEnabled = GetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
            RearView.MPMenuManagerEnabled = GetBool("MP_PM_REARVIEW", RearView.MPMenuManagerEnabled);
            RearView.MenuManagerEnabled = GetBool("O_PM_REARVIEW", RearView.MenuManagerEnabled);
            SuddenDeath.Enabled = GetBool("MP_PM_SUDDENDEATH", SuddenDeath.Enabled);
            ExtMenuManager.mms_ext_lap_limit = GetInt("MP_PM_LAP_LIMIT", ExtMenuManager.mms_ext_lap_limit);
            Console.KeyEnabled = GetBool("O_CONSOLE_KEY", Console.KeyEnabled);
            Console.CustomUIColor = GetInt("O_CUSTOM_UI_COLOR", Console.CustomUIColor);
            Menus.mms_damage_numbers = GetBool("MP_DAMAGE_NUMBERS", Menus.mms_damage_numbers);
            Menus.mms_client_damage_numbers = GetBool("MP_CLIENT_DAMAGE_NUMBERS", Menus.mms_client_damage_numbers);
            ThunderboltPassthrough.Enabled = GetBool("MP_THUNDERBOLT_PASSTHROUGH", ThunderboltPassthrough.Enabled);
            Menus.mms_always_cloaked = GetBool("MP_ALWAYS_CLOAKED", Menus.mms_always_cloaked);
            Menus.mms_classic_spawns = GetBool("MP_CLASSIC_SPAWNS", Menus.mms_classic_spawns);
            Menus.mms_assist_scoring = GetBool("MP_ASSIST_SCORING", Menus.mms_assist_scoring);
            Menus.mms_allow_smash = GetBool("MP_ALLOW_SMASH", Menus.mms_allow_smash);

            JoystickRotationFix.alt_turn_ramp_mode = GetBool("SCALE_UP_ROTATION", JoystickRotationFix.alt_turn_ramp_mode);
            MPColoredPlayerNames.isActive = GetBool("MP_COLORED_NAMES", MPColoredPlayerNames.isActive);
            DisableProfanityFilter.profanity_filter = GetBool("MP_PROFANITY_FILTER", DisableProfanityFilter.profanity_filter);
            Menus.mms_scale_respawn_time = GetBool("MP_PM_SCALE_RESPAWN_TIME", Menus.mms_scale_respawn_time);
            if (Core.GameMod.HasInternetMatch())
                MPInternet.MenuIPAddress = GetString("MP_PM_IP_ADDRESS", MPInternet.MenuIPAddress);
            Menus.mms_lag_compensation = GetInt("MP_PM_LAG_COMPENSATION", Menus.mms_lag_compensation);
            Menus.mms_lag_compensation_advanced = GetBool("MP_PM_LAG_COMPENSATION_ADVANCED", Menus.mms_lag_compensation_advanced);
            Menus.mms_ship_lag_compensation_max = GetInt("MP_PM_SHIP_LAG_COMPENSATION", Menus.mms_ship_lag_compensation_max);
            Menus.mms_weapon_lag_compensation_max = GetInt("MP_PM_WEAPON_LAG_COMPENSATION", Menus.mms_weapon_lag_compensation_max);
            Menus.mms_lag_compensation_strength = GetInt("MP_PM_LAG_COMPENSATION_STRENGTH", Menus.mms_lag_compensation_strength);
            Menus.mms_lag_compensation_use_interpolation = GetInt("MP_PM_LAG_COMPENSATION_USE_INTERPOLATION", Menus.mms_lag_compensation_use_interpolation);
            Menus.mms_lag_compensation_ship_added_lag = GetInt("MP_PM_LAG_COMPENSATION_SHIP_ADDED_LAG", Menus.mms_lag_compensation_ship_added_lag);
            Menus.mms_ship_lag_compensation_scale = GetInt("MP_PM_SHIP_LAG_COMPENSATION_SCALE", Menus.mms_ship_lag_compensation_scale);
            Menus.mms_weapon_lag_compensation_scale = GetInt("MP_PM_WEAPON_LAG_COMPENSATION_SCALE", Menus.mms_weapon_lag_compensation_scale);
            Menus.mms_lag_compensation_collision_limit = GetInt("MP_PM_SHIP_LAG_COMPENSATION_COLLISION_LIMIT", Menus.mms_lag_compensation_collision_limit);
            Menus.mms_sticky_death_summary = GetBool("MP_PM_STICKY_DEATH_SUMMARY", Menus.mms_sticky_death_summary);
            MPDeathReview.stickyDeathReview = Menus.mms_sticky_death_summary;
            Menus.mms_reduced_ship_explosions = GetBool("MP_PM_REDUCED_SHIP_EXPLOSIONS", Menus.mms_reduced_ship_explosions);
            Menus.mms_damageeffect_alpha_mult = GetInt("MP_PM_DAMAGEEFFECT_ALPHA_MULT", Menus.mms_damageeffect_alpha_mult);
            Menus.mms_damageeffect_drunk_blur_mult = GetInt("MP_PM_DAMAGEEFFECT_DRUNK_BLUR_MULT", Menus.mms_damageeffect_drunk_blur_mult);
            Menus.mms_match_time_limit = GetInt("MP_PM_MATCH_TIME_LIMIT", Menus.mms_match_time_limit);
            Menus.mms_team_color_default = GetBool("MP_PM_TEAM_COLOR_DEFAULT", Menus.mms_team_color_default);
            Menus.mms_team_color_self = GetInt("MP_PM_TEAM_COLOR_SELF", Menus.mms_team_color_self);
            Menus.mms_team_color_enemy = GetInt("MP_PM_TEAM_COLOR_ENEMY", Menus.mms_team_color_enemy);
            Menus.mms_team_health = GetBool("MP_PM_TEAM_HEALTH", Menus.mms_team_health);
            HUDVelocity.MenuManagerEnabled = GetBool("MP_PM_SHOWHUDVELOCITY", HUDVelocity.MenuManagerEnabled);
            Menus.mms_show_framerate = GetBool("MP_PM_SHOWFRAMERATE", Menus.mms_show_framerate);
            Menus.mms_audio_occlusion_strength = GetInt("MP_PM_AUDIO_OCCLUSION_STRENGTH", Menus.mms_audio_occlusion_strength);
            Menus.mms_directional_warnings = GetBool("MP_PM_DIRECTIONAL_WARNINGS", Menus.mms_directional_warnings);
            Menus.mms_loadout_hotkeys = GetInt("MP_PM_LOADOUT_HOTKEYS2", Menus.mms_loadout_hotkeys);

            MPLoadouts.Loadouts[0].weapons[0] = (WeaponType)GetInt("MP_PM_LOADOUT_BOMBER1_W1", (int)MPLoadouts.Loadouts[0].weapons[0]);
            MPLoadouts.Loadouts[0].missiles[0] = (MissileType)GetInt("MP_PM_LOADOUT_BOMBER1_M1", (int)MPLoadouts.Loadouts[0].missiles[0]);
            MPLoadouts.Loadouts[0].missiles[1] = (MissileType)GetInt("MP_PM_LOADOUT_BOMBER1_M2", (int)MPLoadouts.Loadouts[0].missiles[1]);

            MPLoadouts.Loadouts[1].weapons[0] = (WeaponType)GetInt("MP_PM_LOADOUT_GUNNER1_W1", (int)MPLoadouts.Loadouts[1].weapons[0]);
            MPLoadouts.Loadouts[1].weapons[1] = (WeaponType)GetInt("MP_PM_LOADOUT_GUNNER1_W2", (int)MPLoadouts.Loadouts[1].weapons[1]);
            MPLoadouts.Loadouts[1].missiles[0] = (MissileType)GetInt("MP_PM_LOADOUT_GUNNER1_M1", (int)MPLoadouts.Loadouts[1].missiles[0]);

            MPLoadouts.Loadouts[2].weapons[0] = (WeaponType)GetInt("MP_PM_LOADOUT_BOMBER2_W1", (int)MPLoadouts.Loadouts[2].weapons[0]);
            MPLoadouts.Loadouts[2].missiles[0] = (MissileType)GetInt("MP_PM_LOADOUT_BOMBER2_M1", (int)MPLoadouts.Loadouts[2].missiles[0]);
            MPLoadouts.Loadouts[2].missiles[1] = (MissileType)GetInt("MP_PM_LOADOUT_BOMBER2_M2", (int)MPLoadouts.Loadouts[2].missiles[1]);

            MPLoadouts.Loadouts[3].weapons[0] = (WeaponType)GetInt("MP_PM_LOADOUT_GUNNER2_W1", (int)MPLoadouts.Loadouts[1].weapons[0]);
            MPLoadouts.Loadouts[3].weapons[1] = (WeaponType)GetInt("MP_PM_LOADOUT_GUNNER2_W2", (int)MPLoadouts.Loadouts[1].weapons[1]);
            MPLoadouts.Loadouts[3].missiles[0] = (MissileType)GetInt("MP_PM_LOADOUT_GUNNER2_M1", (int)MPLoadouts.Loadouts[1].missiles[0]);
        }

        public static void SavePreferences(string filename) {
            DeleteAll();
            SetInt("MP_PM_TEAM_COUNT", Teams.MenuManagerTeamCount);
            SetBool("MP_PM_JIP", MPJoinInProgress.MenuManagerEnabled);
            SetBool("MP_PM_REARVIEW", RearView.MPMenuManagerEnabled);
            SetBool("O_PM_REARVIEW", RearView.MenuManagerEnabled);
            SetBool("MP_PM_SUDDENDEATH", SuddenDeath.Enabled);
            SetInt("MP_PM_LAP_LIMIT", ExtMenuManager.mms_ext_lap_limit);
            SetBool("O_CONSOLE_KEY", Console.KeyEnabled);
            SetInt("O_CUSTOM_UI_COLOR", Console.CustomUIColor);
            SetBool("SCALE_UP_ROTATION", JoystickRotationFix.alt_turn_ramp_mode);
            SetBool("MP_COLORED_NAMES", MPColoredPlayerNames.isActive);
            SetBool("MP_PROFANITY_FILTER", DisableProfanityFilter.profanity_filter);
            SetString("MP_PM_IP_ADDRESS", MPInternet.MenuIPAddress);
            SetBool("MP_PM_SCALE_RESPAWN_TIME", Menus.mms_scale_respawn_time);
            SetInt("MP_PM_LAG_COMPENSATION", Menus.mms_lag_compensation);
            SetBool("MP_PM_LAG_COMPENSATION_ADVANCED", Menus.mms_lag_compensation_advanced);
            SetInt("MP_PM_SHIP_LAG_COMPENSATION", Menus.mms_ship_lag_compensation_max);
            SetInt("MP_PM_WEAPON_LAG_COMPENSATION", Menus.mms_weapon_lag_compensation_max);
            SetInt("MP_PM_LAG_COMPENSATION_STRENGTH", Menus.mms_lag_compensation_strength);
            SetInt("MP_PM_LAG_COMPENSATION_USE_INTERPOLATION", Menus.mms_lag_compensation_use_interpolation);
            SetInt("MP_PM_LAG_COMPENSATION_SHIP_ADDED_LAG", Menus.mms_lag_compensation_ship_added_lag);
            SetInt("MP_PM_WEAPON_LAG_COMPENSATION_SCALE", Menus.mms_weapon_lag_compensation_scale);
            SetInt("MP_PM_SHIP_LAG_COMPENSATION_SCALE", Menus.mms_ship_lag_compensation_scale);
            SetInt("MP_PM_SHIP_LAG_COMPENSATION_COLLISION_LIMIT", Menus.mms_lag_compensation_collision_limit);
            SetBool("MP_PM_STICKY_DEATH_SUMMARY", Menus.mms_sticky_death_summary);
            SetBool("MP_PM_REDUCED_SHIP_EXPLOSIONS", Menus.mms_reduced_ship_explosions);
            SetInt("MP_PM_DAMAGEEFFECT_ALPHA_MULT", Menus.mms_damageeffect_alpha_mult);
            SetInt("MP_PM_DAMAGEEFFECT_DRUNK_BLUR_MULT", Menus.mms_damageeffect_drunk_blur_mult);
            SetInt("MP_PM_MATCH_TIME_LIMIT", Menus.mms_match_time_limit);
            SetBool("MP_PM_TEAM_COLOR_DEFAULT", Menus.mms_team_color_default);
            SetInt("MP_PM_TEAM_COLOR_SELF", Menus.mms_team_color_self);
            SetInt("MP_PM_TEAM_COLOR_ENEMY", Menus.mms_team_color_enemy);
            SetBool("MP_PM_TEAM_HEALTH", Menus.mms_team_health);
            SetBool("MP_PM_SHOWHUDVELOCITY", HUDVelocity.MenuManagerEnabled);
            SetBool("MP_PM_SHOWFRAMERATE", Menus.mms_show_framerate);
            SetInt("MP_PM_AUDIO_OCCLUSION_STRENGTH", Menus.mms_audio_occlusion_strength);
            SetBool("MP_PM_DIRECTIONAL_WARNINGS", Menus.mms_directional_warnings);
            SetInt("MP_PM_LOADOUT_HOTKEYS2", Menus.mms_loadout_hotkeys);
            SetInt("MP_PM_LOADOUT_BOMBER1_W1", (int)MPLoadouts.Loadouts[0].weapons[0]);
            SetInt("MP_PM_LOADOUT_BOMBER1_M1", (int)MPLoadouts.Loadouts[0].missiles[0]);
            SetInt("MP_PM_LOADOUT_BOMBER1_M2", (int)MPLoadouts.Loadouts[0].missiles[1]);
            SetInt("MP_PM_LOADOUT_GUNNER1_W1", (int)MPLoadouts.Loadouts[1].weapons[0]);
            SetInt("MP_PM_LOADOUT_GUNNER1_W2", (int)MPLoadouts.Loadouts[1].weapons[1]);
            SetInt("MP_PM_LOADOUT_GUNNER1_M1", (int)MPLoadouts.Loadouts[1].missiles[0]);
            SetInt("MP_PM_LOADOUT_BOMBER2_W1", (int)MPLoadouts.Loadouts[2].weapons[0]);
            SetInt("MP_PM_LOADOUT_BOMBER2_M1", (int)MPLoadouts.Loadouts[2].missiles[0]);
            SetInt("MP_PM_LOADOUT_BOMBER2_M2", (int)MPLoadouts.Loadouts[2].missiles[1]);
            SetInt("MP_PM_LOADOUT_GUNNER2_W1", (int)MPLoadouts.Loadouts[3].weapons[0]);
            SetInt("MP_PM_LOADOUT_GUNNER2_W2", (int)MPLoadouts.Loadouts[3].weapons[1]);
            SetInt("MP_PM_LOADOUT_GUNNER2_M1", (int)MPLoadouts.Loadouts[3].missiles[0]);
            SetBool("MP_DAMAGE_NUMBERS", Menus.mms_damage_numbers);
            SetBool("MP_CLIENT_DAMAGE_NUMBERS", Menus.mms_client_damage_numbers);
            SetBool("MP_THUNDERBOLT_PASSTHROUGH", ThunderboltPassthrough.Enabled);
            SetBool("MP_ALWAYS_CLOAKED", Menus.mms_always_cloaked);
            SetBool("MP_CLASSIC_SPAWNS", Menus.mms_classic_spawns);
            SetBool("MP_ASSIST_SCORING", Menus.mms_assist_scoring);
            SetBool("MP_ALLOW_SMASH", Menus.mms_allow_smash);

            Flush(filename + "mod");
        }
    }
}
