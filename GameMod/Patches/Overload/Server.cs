using GameMod.Messages;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;

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
