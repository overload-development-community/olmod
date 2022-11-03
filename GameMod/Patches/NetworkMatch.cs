using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    public static class MPTweaks_NetworkMatch_CanLaunchCountdown {
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
    public static class NetworkMatch_ExitMatch {
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
    /// Gets the highest team score in a monsterball match.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScorePowercore")]
    public static class NetworkMatch_GetHighestScorePowercore {
        public static bool Prefix(ref int __result) {
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || Teams.NetworkMatchTeamCount == 2)
                return true;
            __result = Teams.HighestScore();
            return false;
        }
    }

    /// <summary>
    /// Gets the highest team score in a team anarchy match.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "GetHighestScoreTeamAnarchy")]
    public static class NetworkMatch_GetHighestScoreTeamAnarchy {
        public static bool Prefix(ref int __result) {
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || Teams.NetworkMatchTeamCount == 2)
                return true;
            __result = Teams.HighestScore();
            return false;
        }
    }

    /// <summary>
    /// Gets the team name.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "GetTeamName")]
    public static class NetworkMatch_GetTeamName {
        public static bool Prefix(MpTeam team, ref string __result) {
            __result = Teams.TeamName(team);
            return false;
        }
    }

    /// <summary>
    /// Allows game to start even if teams are severely unbalanced.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch]
    public static class NetworkMatch_HostActiveMatchInfo_CanStartNow {
        public static MethodBase TargetMethod() {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetMethod("CanStartNow", BindingFlags.Public | BindingFlags.Instance);
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> cs) {
            foreach (var c in cs) {
                if (c.opcode == OpCodes.Ldsfld && ((FieldInfo)c.operand).Name == "m_match_mode") {
                    var c2 = new CodeInstruction(OpCodes.Ldc_I4_1) { labels = c.labels };
                    yield return c2;
                    yield return new CodeInstruction(OpCodes.Ret);
                    c.labels = null;
                }
                yield return c;
            }
        }
    }

    /// <summary>
    /// Resets the team scores at the start of the game.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "Init")]
    public static class NetworkMatch_Init {
        public static void Prefix() {
            NetworkMatch.m_team_scores = new int[(int)Teams.MPTEAM_NUM];
        }
    }

    /// <summary>
    /// Initialize the tweaks before each match, and resets the team scores.
    /// </summary>
    [Mod(new Mods[] { Mods.Teams, Mods.Tweaks })]
    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    public static class NetworkMatch_InitBeforeEachMatch {
        public static void Postfix() {
            Tweaks.InitMatch();

            for (int i = 0, l = NetworkMatch.m_team_scores.Length; i < l; i++)
                NetworkMatch.m_team_scores[i] = 0;
        }
    }

    /// <summary>
    /// Balances teams when new players join.
    /// </summary>
    [Mod(Mods.Teams)]
    [HarmonyPatch(typeof(NetworkMatch), "NetSystemGetTeamForPlayer")]
    public static class NetworkMatch_NetSystemGetTeamForPlayer {
        public static bool Prefix(ref MpTeam __result, int connection_id) {
            if (!NetworkMatch.IsTeamMode(NetworkMatch.GetMode())) {
                __result = MpTeam.ANARCHY;
                return false;
            }
            if (NetworkMatch.GetMode() == MatchMode.ANARCHY || (Teams.NetworkMatchTeamCount == 2 &&
                !MPJoinInProgress.NetworkMatchEnabled)) // use this simple balancing method for JIP to hopefully solve JIP team imbalances
                return true;
            if (NetworkMatch.m_players.TryGetValue(connection_id, out var connPlayer)) // keep team if player already exists (when called from OnUpdateGameSession)
            {
                __result = connPlayer.m_team;
                return false;
            }
            int[] team_counts = new int[(int)Teams.MPTEAM_NUM];
            foreach (var player in NetworkMatch.m_players.Values)
                team_counts[(int)player.m_team]++;
            MpTeam min_team = MpTeam.TEAM0;
            foreach (var team in Teams.GetTeams)
                if (team_counts[(int)team] < team_counts[(int)min_team] ||
                    (team_counts[(int)team] == team_counts[(int)min_team] &&
                        NetworkMatch.m_team_scores[(int)team] < NetworkMatch.m_team_scores[(int)min_team]))
                    min_team = team;
            __result = min_team;
            Debug.LogFormat("GetTeamForPlayer: result {0}, conn {1}, counts {2}, scores {3}", (int)min_team, connection_id,
                team_counts.Join(), NetworkMatch.m_team_scores.Join());
            return false;
        }
    }

    /// <summary>
    /// Update lobby status display.
    /// </summary>
    [Mod(Mods.PresetData)]
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    public static class NetworkMatch_OnAcceptedToLobby_PresetData {
        public static void Postfix() {
            PresetData.UpdateLobbyStatus();
        }
    }

    /// <summary>
    /// Send client capabilities for compatibility.
    /// </summary>
    [Mod(Mods.Tweaks)]
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    public static class NetworkMatch_OnAcceptedToLobby_Tweaks {
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
    public static class NetworkMatch_StartLobby {
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
    /// Reports the start of the game to the tracker, and resets the team scores.
    /// </summary>
    [Mod(new Mods[] { Mods.Teams, Mods.Tracker })]
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    public static class NetworkMatch_StartPlaying {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix() {
            if (NetworkManager.IsHeadless()) {
                Tracker.StartGame();
            }

            for (int i = 0, l = NetworkMatch.m_team_scores.Length; i < l; i++)
                NetworkMatch.m_team_scores[i] = 0;
        }
    }

    /// <summary>
    /// Disables updating of Gamelift pings.
    /// </summary>
    [Mod(Mods.DisableGamelift)]
    [HarmonyPatch(typeof(NetworkMatch), "UpdateGameliftPings")]
    public static class NetworkMatch_UpdateGameliftPings {
        public static bool Prefix() {
            return false;
        }
    }
}
