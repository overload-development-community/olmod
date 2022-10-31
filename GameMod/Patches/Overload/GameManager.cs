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
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Postfix() {
            AudioListener.pause = true;
            Debug.Log("Dedicated Server: paused AudioListener");
        }
    }

    /// <summary>
    /// Enables or disables the ability to hear the server's audio.
    /// </summary>
    /// <remarks>
    /// Change the value of enableAudio to true to hear the server's audio.
    /// </remarks>
    [Mod(Mods.ServerCleanup)]
    [HarmonyPatch(typeof(GameManager), "Update")]
    public class GameManager_Update {
        private const bool enableAudio = false;

        public static bool Prepare() {
            return enableAudio && GameplayManager.IsDedicatedServer();
        }

        public static void Postfix() {
            AudioListener.volume = 1.0f;
        }
    }
}
