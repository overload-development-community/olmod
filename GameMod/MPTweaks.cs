using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameMod.Messages;
using GameMod.Objects;
using GameMod.VersionHandling;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod {
    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MPTweaksInitBeforeEachMatch
    {
        private static void Postfix()
        {
            Tweaks.InitMatch();
        }
    }

    /// <summary>
    /// Sends map-specific tweaks to the client.
    /// </summary>
    /// <remarks>
    /// This should only be used for map-specific tweaks.  Use client capabilities in NetworkMatch.OnAcceptedToLobby for general tweaks.
    /// </remarks>
    [HarmonyPatch(typeof(Overload.NetworkManager), "LoadScene")]
    class MPTweaksLoadScene
    {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        private static void Postfix()
        {
            RobotManager.ReadMultiplayerModeFile();

            var tweaks = new Dictionary<string, string>() { };

            if (!MPCustomModeFile.PickupCheck)
                tweaks.Add("item.pickupcheck", bool.FalseString);

            if (tweaks.Any())
            {
                Settings.Set(tweaks);
                Settings.Send();
            }
        }
    }

    [HarmonyPatch(typeof(GameplayManager), "StartLevel")]
    class MPTweaksStartLevel
    {
        private static void Postfix()
        {
            if (!GameplayManager.IsMultiplayerActive)
                Settings.Reset();
            Settings.Apply();
        }
    }

    // clear client capabilities on connect in case connection is reused
    [HarmonyPatch(typeof(Server), "OnConnect")]
    class MPTweaksServerOnConnect
    {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        static void Postfix(NetworkMessage msg)
        {
            Tweaks.ClientCapabilitiesRemove(msg.conn.connectionId);
        }
    }

    // send client capabilities for compatible server
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    class MPTweaksAcceptedToLobby
    {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        private static void Prefix(AcceptedToLobbyMessage accept_msg)
        {
            if (Client.GetClient() == null)
            {
                return;
            }

            var server = accept_msg.m_server_location;
            if (!server.StartsWith("OLMOD "))
            {
                // other server / server too old
                Debug.LogFormat("MPTweaks: unsupported server {0}", server);
                return;
            }

            var caps = new Dictionary<string, string> {
                { "ModVersion", OlmodVersion.RunningVersion.ToString(OlmodVersion.RunningVersion.Revision == 0 ? 3 : 4) },
                { "ModFullVersion", OlmodVersion.FullVersionString },
                { "Modded", OlmodVersion.Modded ? "1" : "0" },
                { "ModsLoaded", Core.GameMod.ModsLoaded },
                { "SupportsTweaks", "changeteam,deathreview,sniper,jip,nocompress_0_3_6,customloadouts,damagenumbers" }
            };
            Client.GetClient().Send(MessageTypes.MsgClientCapabilities, new TweaksMessage { m_settings = caps } );
        }
    }

    // no capabilities seen when OnLoadoutDataMessage arrives? -> old client
    [HarmonyPatch(typeof(Server), "OnLoadoutDataMessage")]
    class MPTweaksOnLoadoutDataMessage
    {
        public static bool Prepare() {
            return GameplayManager.IsDedicatedServer();
        }

        private static IEnumerator DisconnectCoroutine(int connectionId)
        {
            yield return new WaitForSecondsRealtime(5);
            var conn = NetworkServer.connections[connectionId];
            if (conn != null)
                conn.Disconnect();
        }

        private static bool ClientModifiersValid(int connectionId)
        {
            var conn = NetworkServer.connections[connectionId];
            if (conn != null)
            {
                var player = NetworkMatch.m_player_loadout_data[conn.connectionId];
                if (player != null)
                {
                    return MPModifiers.PlayerModifiersValid(player.m_mp_modifier1, player.m_mp_modifier2);
                }
            }

            return false;
        }

        private static void Postfix(NetworkMessage msg)
        {
            var connId = msg.conn.connectionId;

            if (connId == 0) // ignore local connection
                return;

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
}
