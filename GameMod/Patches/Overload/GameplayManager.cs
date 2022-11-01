using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Determines whether to show rear view based on the gameplay state.
    /// </summary>
    [Mod(Mods.RearView)]
    [HarmonyPatch(typeof(GameplayManager), "ChangeGameplayState")]
    public class GameplayManager_ChangeGameplayState {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Prefix(GameplayState new_state) {
            if (new_state != GameplayState.PLAYING) {
                RearView.Pause();
            } else if (new_state == GameplayState.PLAYING) {
                if (RearView.Enabled)
                    RearView.Init();
                else
                    RearView.Pause();
            }
        }
    }

    /// <summary>
    /// Heavy-handed, re-init projdatas & robotdatas on scene loaded.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(GameplayManager), "OnSceneLoaded")]
    public class GameplayManager_OnSceneLoaded {
        public static void LoadCustomPresets() {
            ProjectileManager.ReadProjPresetData(ProjectileManager.proj_prefabs);
            RobotManager.ReadPresetData(RobotManager.m_enemy_prefab);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(GameplayManager), "StartLevel"))
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GameplayManager_OnSceneLoaded), "LoadCustomPresets"));
                yield return code;
            }
        }
    }

    /// <summary>
    /// Applies the tweak settings at the start of a level.
    /// </summary>
    [Mod(Mods.Tweaks)]
    [HarmonyPatch(typeof(GameplayManager), "StartLevel")]
    public class GameplayManager_StartLevel {
        public static void Postfix() {
            if (!GameplayManager.IsMultiplayerActive)
                Settings.Reset();
            Settings.Apply();
        }
    }
}
