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
                Application.targetFrameRate = -1;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawMainMenu")]
    class GSyncFix_DrawMainMenu {
        static void Postfix() {
            if (Application.targetFrameRate == -1) {
                Application.targetFrameRate = 120;
            }
        }
    }
}
