using GameMod.Metadata;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Pauses the AudioListener.
    /// </summary>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(GameManager), "SetupDedicatedServer")]
    public class GameManager_SetupDedicatedServer {
        public static void Postfix() {
            if (GameplayManager.IsDedicatedServer()) {
                AudioListener.pause = true;
                Debug.Log("Dedicated Server: paused AudioListener");
            }
        }
    }

    /// <summary>
    /// Enables or disables the ability to hear the server's audio.
    /// </summary>
    /// <remarks>
    /// Change the return of Prepare() to true to hear the server's audio.
    /// </remarks>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(GameManager), "Update")]
    public class GameManager_Update {
        public static bool Prepare() {
            return false;
        }

        public static void Postfix() {
            if (GameplayManager.IsDedicatedServer()) {
                AudioListener.volume = 1.0f;
            }
        }
    }
}
