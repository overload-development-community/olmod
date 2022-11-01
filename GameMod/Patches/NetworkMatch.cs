using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Messages;
using GameMod.Metadata;
using GameMod.Objects;
using GameMod.VersionHandling;
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
    /// Initialize the tweaks before each match.
    /// </summary>
    [Mod(Mods.Tweaks)]
    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    public class NetworkMatch_InitBeforeEachMatch {
        public static void Postfix() {
            Tweaks.InitMatch();
        }
    }

    /// <summary>
    /// Update lobby status display.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    public class NetworkMatch_OnAcceptedToLobby_PresetData {
        public static void Postfix() {
            PresetData.UpdateLobbyStatus();
        }
    }

    /// <summary>
    /// Send client capabilities for compatibility.
    /// </summary>
    [Mod(Mods.Tweaks)]
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    public class NetworkMatch_OnAcceptedToLobby_Tweaks {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static void Prefix(AcceptedToLobbyMessage accept_msg) {
            if (Client.GetClient() == null) {
                return;
            }

            var server = accept_msg.m_server_location;
            if (!server.StartsWith("OLMOD ")) {
                // other server / server too old
                Debug.LogFormat("MPTweaks: unsupported server {0}", server);
                return;
            }

            var caps = new Dictionary<string, string> {
                { "ModVersion", OlmodVersion.RunningVersion.ToString(OlmodVersion.RunningVersion.Revision == 0 ? 3 : 4) },
                { "ModFullVersion", OlmodVersion.FullVersionString },
                { "Modded", OlmodVersion.Modded ? "1" : "0" },
                { "ModsLoaded", Core.GameMod.ModsLoaded },
                { "SupportsTweaks", "" }
            };
            Client.GetClient().Send(MessageTypes.MsgClientCapabilities, new TweaksMessage { m_settings = caps });
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
