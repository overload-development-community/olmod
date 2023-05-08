using HarmonyLib;
using Newtonsoft.Json.Linq;
using Overload;
using System.Collections;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(NetworkMatch), "StartLobby")]
    class ServerTrackerPost
    {
        private static bool started = false;
        private static string url;

        public static void Postfix()
        {
            if (!Config.Settings.Value<bool>("isServer") ||
                string.IsNullOrEmpty(url = Config.Settings.Value<string>("trackerBaseUrl")))
                return;

            if (!started && NetworkManager.IsHeadless())
            {
                started = true;
                GameManager.m_gm.StartCoroutine(PingRoutine());
            }
        }

        public static IEnumerator PingRoutine()
        {
            for (;;) {
                ServerStatLog.TrackerPost("/api/ping", JObject.FromObject(new
                {
                    keepListed = Config.Settings.Value<bool>("keepListed"),
                    name = Config.Settings.Value<string>("serverName"),
                    notes = Config.Settings.Value<string>("notes"),
                    version = VersionHandling.OlmodVersion.FullVersionString
                }));
                yield return new WaitForSecondsRealtime(5 * 60);
            }
        }
    }

    [HarmonyPatch(typeof(Server), "SendPlayersInLobbyToAllClients")]
    class ServerLobbyStatus
    {
        public static void SendToTracker()
        {
            MatchState state = NetworkMatch.GetMatchState();
            if (state != MatchState.LOBBY && state != MatchState.LOBBY_LOADING_SCENE && state != MatchState.LOBBY_LOAD_COUNTDOWN)
                return;

            var obj = ServerStatLog.GetGameData();
            obj["name"] = "Stats";
            obj["type"] = "LobbyStatus";

            ServerStatLog.TrackerPostStats(obj);
        }

        public static void Postfix()
        {
            SendToTracker();
        }
    }
}
