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
    /// Stock game shows 60/30hz for what is actually "full/half" sync rates in Unity, simply change labels
    /// </summary>
    [Mod(Mods.VSync)]
    [HarmonyPatch(typeof(MenuManager), "GetVSyncSetting")]
    public class MenuManager_GetVSyncSetting {
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
    public class MenuManager_GraphicsOptionsUpdate {
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
    /// Update lobby status display.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    public class MenuManager_MpMatchSetup {
        public static void Postfix() {
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE) {
                if (MenuManager.m_menu_micro_state != 2) {
                    PresetData.UpdateLobbyStatus();
                }
            }
        }
    }

    /// <summary>
    /// Don't show level loading screen on server, which is prone to crashing.
    /// </summary>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(MenuManager), "PlayGameUpdate")]
    public class MenuManager_PlayGameUpdate {
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
}
