using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches {
    /// <summary>
    /// Set players to start to 1 if it is not already, and send a valid match mode to avoid a crash.
    /// </summary>
    [Mod(new Mods[] { Mods.OnePlayerMultiplayerGames, Mods.Race })]
    [HarmonyPatch(typeof(MenuManager), "BuildPrivateMatchData")]
    public static class MenuManager_BuildPrivateMatchData {
        public static void Postfix(PrivateMatchDataMessage __result) {
            if (__result.m_num_players_to_start != 1)
                __result.m_num_players_to_start = 1;
            if ((int)__result.m_match_mode > (int)ExtMatchMode.CTF) // Newer matchmodes must be sent with ModPrivateData.
                __result.m_match_mode = NetworkMatch.IsTeamMode(__result.m_match_mode) ? MatchMode.TEAM_ANARCHY : MatchMode.ANARCHY;
        }
    }

    /// <summary>
    /// Gets the team name.
    /// </summary>
    [Mod(new Mods[] { Mods.CustomTeamColors, Mods.Teams })]
    [HarmonyPatch(typeof(MenuManager), "GetMpTeamName")]
    public static class MenuManager_GetMpTeamName {
        public static bool Prefix(MpTeam team, ref string __result) {
            if (!GameplayManager.IsMultiplayerActive) {
                switch (team) {
                    case MpTeam.TEAM0:
                        __result = Loc.LS("BLUE TEAM / TEAM 1");
                        break;
                    case MpTeam.TEAM1:
                        __result = Loc.LS("ORANGE TEAM / TEAM 2");
                        break;
                    case MpTeam.ANARCHY:
                        __result = Loc.LS("ANARCHY");
                        break;
                    default:
                        __result = Loc.LS("UNKNOWN");
                        break;
                }
            } else {
                __result = Teams.TeamName(team) + " TEAM";
            }

            return false;
        }
    }

    /// <summary>
    /// Stock game shows 60/30hz for what is actually "full/half" sync rates in Unity, simply change labels
    /// </summary>
    [Mod(Mods.VSync)]
    [HarmonyPatch(typeof(MenuManager), "GetVSyncSetting")]
    public static class MenuManager_GetVSyncSetting {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "60 HZ")
                    code.operand = "100% MONITOR RATE";

                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "30 HZ")
                    code.operand = "50% MONITOR RATE";

                yield return code;
            }
        }
    }

    /// <summary>
    /// Stock game did not actually implement reverse arrow on vsync option.
    /// </summary>
    /// <remarks>
    /// Original: MenuManager.gfx_vsync = (MenuManager.gfx_vsync + 3 - 1) % 3
    /// New:      MenuManager.gfx_vsync = (MenuManager.gfx_vsync + 3 + 1 - 1 + UIManager.m_select_dir) % 3
    /// </remarks>
    [Mod(Mods.VSync)]
    [HarmonyPatch(typeof(MenuManager), "GraphicsOptionsUpdate")]
    public static class MenuManager_GraphicsOptionsUpdate {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {

                if (state == 0 && code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(MenuManager), "gfx_vsync"))
                    state = 1;

                if (state == 1 && code.opcode == OpCodes.Ldc_I4_3) {
                    yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                    yield return new CodeInstruction(OpCodes.Add);
                    yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(UIManager), "m_select_dir"));
                    yield return new CodeInstruction(OpCodes.Add);
                    state = 2;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Initializes some settings.
    /// </summary>
    [Mod(Mods.ModPreferences)]
    [HarmonyPatch(typeof(MenuManager), "InitMpPrivateMatch")]
    public static class MenuManager_InitMpPrivateMatch {
        public static void Postfix() {
            ModPreferences.InitSettings();
        }
    }

    /// <summary>
    /// Loads modded preferences.
    /// </summary>
    [Mod(Mods.ModPreferences)]
    [HarmonyPatch(typeof(MenuManager), "LoadPreferences")]
    public static class MenuManager_LoadPreferences {
        public static void Postfix() {
            ModPreferences.LoadPreferences();
        }
    }

    /// <summary>
    /// Update lobby status display.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    public static class MenuManager_MpMatchSetup {
        public static void Postfix() {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE) {
                if (MenuManager.m_menu_micro_state != 2) {
                    PresetData.UpdateLobbyStatus();
                }
            }
        }
    }

    /// <summary>
    /// Allows pasting of the password with Ctrl+V.
    /// </summary>
    [Mod(Mods.PasswordPaste)]
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    public static class MenuManager_MpMatchSetup_PasswordPaste {
        public static bool Prepare() {
            return !Core.GameMod.HasInternetMatch() && !GameplayManager.IsDedicatedServer();
        }

        public static void Prefix() {
            if ((MenuManager.m_menu_micro_state == 1 || MenuManager.m_menu_micro_state == 4) && UIManager.m_menu_selection == 11 &&
                ((Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) && Input.GetKeyDown(KeyCode.V)))
                MPInternet.MenuPassword = GUIUtility.systemCopyBuffer;
        }
    }

    /// <summary>
    /// Reduces the wait time to switch teams.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(MenuManager), "MpPreMatchMenuUpdate")]
    public static class MenuManager_MpPreMatchMenuUpdate {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            var mpTeams_NextTeam_Method = AccessTools.Method(typeof(Teams), "NextTeam");

            for (var codes = instructions.GetEnumerator(); codes.MoveNext();) {
                var code = codes.Current;
                if (code.opcode == OpCodes.Ldc_R4 && (float)code.operand == 3f) // reduce team switch wait time
                    code.operand = 0.2f;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == "m_team") {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, mpTeams_NextTeam_Method);
                    // skip until RequestSwitchTeam call
                    while (codes.MoveNext() && codes.Current.opcode != OpCodes.Call)
                        ;
                    code = codes.Current;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Don't show level loading screen on server, which is prone to crashing.
    /// </summary>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(MenuManager), "PlayGameUpdate")]
    public static class MenuManager_PlayGameUpdate {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        private static readonly FieldInfo _MenuManager_m_seconds_waiting_for_gi_covergence = typeof(MenuManager).GetField("m_seconds_waiting_for_gi_covergence", BindingFlags.Static | BindingFlags.NonPublic);
        private static readonly MethodInfo _MenuManager_ResetBackStack = AccessTools.Method(typeof(MenuManager), "ResetBackStack");
        public static bool Prefix(bool returning_from_secret) {
            if (MenuManager.m_menu_sub_state != MenuSubState.INIT) {
                return true;
            }

            MenuManager.m_returning_from_secret = returning_from_secret;
            GameplayManager.m_game_time_mission = (float)GameplayManager.m_game_time_mission + (Time.realtimeSinceStartup - GameplayManager.m_between_level_start);
            if (GameplayManager.LevelIsLoading()) {
                GameplayManager.CompleteLevelLoad();
            }
            if (GameplayManager.LevelIsLoading()) {
                GameplayManager.CompleteLevelLoad();
            } else {
                GameplayManager.LoadLevel(GameplayManager.m_level_info);
                GameplayManager.AllowSceneActivation();
            }
            MenuManager.m_menu_sub_state = MenuSubState.ACTIVE;
            _MenuManager_m_seconds_waiting_for_gi_covergence.SetValue(null, 0f);
            _MenuManager_ResetBackStack.Invoke(null, new object[] { });

            return false;
        }
    }

    /// <summary>
    /// Saves modded preferences, and fixes a bug with XP resetting.
    /// </summary>
    [Mod(new Mods[] { Mods.ModPreferences, Mods.XPResetFix })]
    [HarmonyPatch(typeof(MenuManager), "SavePreferences")]
    public static class MenuManager_SavePreferences {
        private static int lastXP;

        [Mod(Mods.XPResetFix)]
        public static void Store() {
            if (MenuManager.LocalGetInt("PS_XP2", 0) == 0 && lastXP > 0)
                MenuManager.LocalSetInt("PS_XP2", lastXP);
        }

        [Mod(Mods.XPResetFix)]
        public static void Prefix(string filename) {
            lastXP = MenuManager.LocalGetInt("PS_XP2", 0);

            ModPreferences.SavePreferences(filename);
        }

        [Mod(new Mods[] { Mods.ModPreferences, Mods.XPResetFix })]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            var mpSetupSave_Store_Method = AccessTools.Method(typeof(MenuManager_SavePreferences), "Store");
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == "Flush")
                    yield return new CodeInstruction(OpCodes.Call, mpSetupSave_Store_Method);
                yield return code;
            }
        }
    }

    /// <summary>
    /// Sets defaults for some settings.
    /// </summary>
    [Mod(Mods.ModPreferences)]
    [HarmonyPatch(typeof(MenuManager), "SetPreferencesDefaults")]
    public static class MenuManager_SetPreferencesDefaults {
        public static void Postfix() {
            ModPreferences.InitSettings();
            ModPreferences.SetDefaults();
        }
    }
}
