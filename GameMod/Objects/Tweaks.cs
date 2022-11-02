using System.Collections.Generic;
using System.Linq;
using GameMod.Metadata;
using HarmonyLib;
using UnityEngine;

namespace GameMod.Objects {
    /// <summary>
    /// A class to handle the setting and getting of client tweaks.
    /// </summary>
    [Mod(Mods.Tweaks)]
    public static class Tweaks {
        private class ClientInfo {
            public Dictionary<string, string> Capabilities = new Dictionary<string, string>();
            public HashSet<string> Tweaks = new HashSet<string>();
        }

        private static readonly Dictionary<int, ClientInfo> ClientInfos = new Dictionary<int, ClientInfo>();

        /// <summary>
        /// Removes a client's capabiltiies, usually because a new client is connecting.
        /// </summary>
        /// <param name="connectionId"></param>
        public static void ClientCapabilitiesRemove(int connectionId) {
            ClientInfos.Remove(connectionId);
        }

        /// <summary>
        /// Sets a client's capabilities.  Set either by the TweaksMessage handler or when the server detects the client has no capabilities.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="capabilities"></param>
        public static void ClientCapabilitiesSet(int connectionId, Dictionary<string, string> capabilities) {
            ClientInfos[connectionId] = new ClientInfo { Capabilities = capabilities };
            if (capabilities.TryGetValue("SupportsTweaks", out string supportsTweaks)) {
                if (!string.IsNullOrEmpty(supportsTweaks)) {
                    foreach (var tweak in supportsTweaks.Split(',')) {
                        ClientInfos[connectionId].Tweaks.Add(tweak);
                    }
                }
            }

            Debug.Log($"MPTweaks: conn {connectionId} clientInfo is now {ClientInfos[connectionId].Capabilities.Join()}");
        }

        /// <summary>
        /// Checks whether a client has any capabilities set yet.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public static bool ClientHasCapabilities(int connectionId) {
            return ClientInfos.ContainsKey(connectionId);
        }

        /// <summary>
        /// Checks whether a client is using olmod.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public static bool ClientHasMod(int connectionId) {
            return ClientInfos.TryGetValue(connectionId, out var clientInfo) &&
                clientInfo.Capabilities.ContainsKey("ModVersion");
        }

        /// <summary>
        /// Checks whether a client has a specific tweak.
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="tweak"></param>
        /// <returns></returns>
        public static bool ClientHasTweak(int connectionId, string tweak) {
            return ClientInfos.TryGetValue(connectionId, out var clientInfo) &&
                clientInfo.Tweaks.Contains(tweak);
        }

        /// <summary>
        /// Initializes the match, resetting the settings and clearing all client info.
        /// </summary>
        public static void InitMatch() {
            Settings.Reset();
            ClientInfos.Clear();
        }

        /// <summary>
        /// These are the conditions where a player joining must have olmod to join.
        /// </summary>
        /// <returns></returns>
        public static bool MatchNeedsMod() {
            return (int)NetworkMatch.GetMode() > (int)MatchMode.TEAM_ANARCHY ||
                (NetworkMatch.IsTeamMode(NetworkMatch.GetMode()) && MPTeams.NetworkMatchTeamCount > 2) ||
                MPClassic.matchEnabled || MPModPrivateData.ClassicSpawnsEnabled;
        }
    }
}