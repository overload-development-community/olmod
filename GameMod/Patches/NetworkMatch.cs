using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches {
    /// <summary>
    /// Doubles the time allotted to wait for a client to start the match.
    /// </summary>
    [Mod(Mods.LaunchCountdown)]
    [HarmonyPatch(typeof(NetworkMatch), "CanLaunchCountdown")]
    public class MPTweaks_NetworkMatch_CanLaunchCountdown {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                if (code.opcode == OpCodes.Ldc_R4) {
                    code.operand = 60f;
                }
                yield return code;
            }
        }
    }

    /// <summary>
    /// Reports the end of the game to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(NetworkMatch), "ExitMatch")]
    public class NetworkMatch_ExitMatch {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix() {
            if (!NetworkManager.IsHeadless())
                return;
            Tracker.EndGame();
        }
    }

    /// <summary>
    /// Update lobby status display.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    public class NetworkMatch_OnAcceptedToLobby {
        public static void Postfix() {
            PresetData.UpdateLobbyStatus();
        }
    }

    /// <summary>
    /// Starts pinging the tracker every 5 minutes.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(NetworkMatch), "StartLobby")]
    public class NetworkMatch_StartLobby {
        private static bool started = false;

        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Postfix() {
            if (!started && NetworkManager.IsHeadless()) {
                started = true;
                GameManager.m_gm.StartCoroutine(PingRoutine());
            }
        }

        public static IEnumerator PingRoutine() {
            while (true) {
                Tracker.Ping();
                yield return new WaitForSecondsRealtime(5 * 60);
            }
        }
    }

    /// <summary>
    /// Reports the start of the game to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    public class NetworkMatch_StartPlaying {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix() {
            if (!NetworkManager.IsHeadless())
                return;
            Tracker.StartGame();
        }
    }

    /// <summary>
    /// Disables updating of Gamelift pings.
    /// </summary>
    [Mod(Mods.DisableGamelift)]
    [HarmonyPatch(typeof(NetworkMatch), "UpdateGameliftPings")]
    public class NetworkMatch_UpdateGameliftPings {
        public static bool Prefix() {
            return false;
        }
    }
}
