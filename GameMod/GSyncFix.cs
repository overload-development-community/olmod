using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod {
    // GSync fix
    [HarmonyPatch(typeof(GameManager), "UpdateTargetFramerate")]
    class GSyncFix_UpdateTargetFramerate {
        static bool Prefix() {
            if (GameplayManager.IsDedicatedServer()) {
                Application.targetFrameRate = 120;
            } else {
                if (GameManager.m_game_state == GameManager.GameState.GAMEPLAY) {
                    Application.targetFrameRate = -1;
                } else {
                    Application.targetFrameRate = 120;
                }
            }
            return false;
        }
    }
}
