using System.Collections;
using System.Collections.Generic;
using GameMod.Messages;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Reports disconnects to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(Server), "InvokeDisconnectFlashOnClients")]
    public class Server_InvokeDisconnectFlashOnClients {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix(Player disconnected_player) {
            if (!disconnected_player.m_spectator) {
                Tracker.Disconnected(disconnected_player.m_mp_name);
            }
        }
    }

    /// <summary>
    /// Sets the server port.
    /// </summary>
    [Mod(Mods.ServerPort)]
    [HarmonyPatch(typeof(Server), "Listen")]
    public class Server_Listen {
        private static int PortArg = 0;

        public static bool Prepare() {
            if (!int.TryParse(Switches.Port, out int val))
                return false;
            PortArg = val;
            return true;
        }

        public static void Prefix(ref int port) {
            if (port == 0)
                port = PortArg;
        }
    }

    /// <summary>
    /// Clear client capabilities on connect in case connection is reused.
    /// </summary>
    [Mod(Mods.Tweaks)]
    [HarmonyPatch(typeof(Server), "OnConnect")]
    public class Server_OnConnect {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        public static void Postfix(NetworkMessage msg) {
            Tweaks.ClientCapabilitiesRemove(msg.conn.connectionId);
        }
    }

    /// <summary>
    /// Prevents joining if olmod is required, or there are disabled modifiers.
    /// </summary>
    [Mod(new Mods[] { Mods.Modifiers, Mods.Tweaks })]
    [HarmonyPatch(typeof(Server), "OnLoadoutDataMessage")]
    public class Server_OnLoadoutDataMessage {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        private static IEnumerator DisconnectCoroutine(int connectionId) {
            yield return new WaitForSecondsRealtime(5);
            var conn = NetworkServer.connections[connectionId];
            if (conn != null)
                conn.Disconnect();
        }

        private static bool ClientModifiersValid(int connectionId) {
            var conn = NetworkServer.connections[connectionId];
            if (conn != null) {
                var player = NetworkMatch.m_player_loadout_data[conn.connectionId];
                if (player != null) {
                    return MPModifiers.PlayerModifiersValid(player.m_mp_modifier1, player.m_mp_modifier2);
                }
            }

            return false;
        }

        public static void Postfix(NetworkMessage msg) {
            var connId = msg.conn.connectionId;

            if (connId == 0) // ignore local connection
                return;

            // No capabilities seen when OnLoadoutDataMessage arrives?  Then they are an old client.
            if (!Tweaks.ClientHasCapabilities(connId)) {
                Tweaks.ClientCapabilitiesSet(connId, new Dictionary<string, string>());
            }

            if (!Tweaks.ClientHasMod(connId) && Tweaks.MatchNeedsMod()) {
                NetworkServer.SendToClient(connId, 86, new StringMessage("This match requires OLMod to play."));
                GameManager.m_gm.StartCoroutine(DisconnectCoroutine(connId));
            }
            if ((NetworkMatch.GetMatchState() != MatchState.LOBBY && NetworkMatch.GetMatchState() != MatchState.LOBBY_LOAD_COUNTDOWN) && !ClientModifiersValid(connId)) {
                NetworkServer.SendToClient(connId, 86, new StringMessage("This match has disabled modifiers.  Please disable these modifiers and try again: " + MPModifiers.GetDisabledModifiers()));
                GameManager.m_gm.StartCoroutine(DisconnectCoroutine(connId));
            }
            if (Tweaks.ClientHasMod(connId)) {
                MPModPrivateDataTransfer.SendTo(connId);
            }
        }
    }

    /// <summary>
    /// Registers all of olmod's server handlers.
    /// </summary>
    [Mod(Mods.MessageHandlers)]
    [HarmonyPatch(typeof(Server), "RegisterHandlers")]
    public class Server_RegisterHandlers {
        public static void Postfix() {
            RegisterHandlers.RegisterServerHandlers();
        }
    }

    /// <summary>
    /// Reports the lobby status to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(Server), "SendPlayersInLobbyToAllClients")]
    public class Server_SendPlayersInLobbyToAllClients {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Postfix() {
            MatchState state = NetworkMatch.GetMatchState();
            if (state != MatchState.LOBBY && state != MatchState.LOBBY_LOADING_SCENE && state != MatchState.LOBBY_LOAD_COUNTDOWN)
                return;

            Tracker.LobbyStatus();
        }
    }
}
