//#define MATCHMAKER_DEBUG_LOG

using HarmonyLib;
using Newtonsoft.Json.Linq;
using Overload;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameMod
{
    // Disable UIManager update for headless server, fixes Tobii log spam
    [HarmonyPatch(typeof(UIManager), "Update")] // UpdateEyePositionBuffer
    class ServerLogReduceTobii
    {
        static bool Prefix()
        {
            return !GameplayManager.IsDedicatedServer();
        }
    }

    // remove newlines from LocalLANHost Log messages to prevent many-line matchmaker json dumps
    [HarmonyPatch(typeof(LocalLANHost), "Log")]
    class ServerLogReduceLocalLANHost
    {
        static void Prefix(ref string message, ref object[] args)
        {
            if (args != null && args.Length > 0)
            {
                message = string.Format(message, args).Replace("\r\n", " ").Replace("\n", " ");
                args = null;
            }
        }
    }

    // Log analytics event
    [HarmonyPatch(typeof(GameManager), "AnalyticsCustomEvent")]
    class LogAnalytics
    {
        private static void Prefix(string customEventName, IDictionary<string, object> eventData)
        {
            Debug.Log("AnalyticsCustomEvent " + customEventName + " " + eventData.Join());
        }
    }

    #if MATCHMAKER_DEBUG_LOG
    // log FindGameSessionToCreate
    [HarmonyPatch(typeof(LocalLANHost), "FindGameSessionToCreate")]
    class MPMaxPlayerFindGameSessionToCreate
    {
        static void Prefix(DistributedMatchUp.Match[] requests, DistributedMatchUp.Match backfillSeedMatch)
        {
            foreach (var m in requests)
                Debug.Log(DateTime.Now.ToString() + ": HOST: FindGameSessionToCreate req " + m.uid + " maxp=" + m.maxPlayers + " #p=" + JArray.Parse(m.matchData["mm_players"].stringValue).Count);
            if (backfillSeedMatch != null) {
                var m = backfillSeedMatch;
                Debug.Log(DateTime.Now.ToString() + ": HOST: FindGameSessionToCreate backfill " + m.uid + " maxp=" + m.maxPlayers + " #p=" + JArray.Parse(m.matchData["mm_players"].stringValue).Count);
            }
        }
    }

    // log CreateMatch
    [HarmonyPatch(typeof(DistributedMatchUp), "CreateMatch")]
    class MPMaxPlayerCreateMatch
    {
        static void Postfix(DistributedMatchUp __instance)
        {
            DistributedMatchUp.Match m = __instance.ActiveMatch;
            Debug.Log(DateTime.Now.ToString() + " " + System.Diagnostics.Process.GetCurrentProcess().Id + ": create match " + m.uid + " updated to " + m.matchData["mm_ticketType"]);
        }
    }

    // log UpdateMatch
    [HarmonyPatch(typeof(DistributedMatchUp), "UpdateMatch")]
    class MPMaxPlayerUpdateMatch
    {
        static void Postfix(DistributedMatchUp __instance)
        {
            DistributedMatchUp.Match m = __instance.ActiveMatch;
            Debug.Log(DateTime.Now.ToString() + " " + System.Diagnostics.Process.GetCurrentProcess().Id + ": update match " + m.uid + " updated to " + m.matchData["mm_ticketType"]);
        }
    }

    // log received broadcast payloads for debugging
    [HarmonyPatch(typeof(DistributedMatchUp.Match), "Deserialize")]
    class MPMaxPlayerReceive
    {
        static void Postfix(DistributedMatchUp.Match __result)
        {
            var m = __result;
            Debug.Log(DateTime.Now.ToString() + " " + System.Diagnostics.Process.GetCurrentProcess().Id + ": received " + (m == null ? "null" : m.uid + " type " + m.matchData["mm_ticketType"] + " tickets " + m.matchData.GetValueSafe("mm_mmTickets")));
        }
    }

    [HarmonyPatch(typeof(DistributedMatchUp), "Overload.IBroadcastStateReceiver.OnClientStateUpdate")]
    class MPMaxPlayerOnClientStateUpdate
    {
        static void Postfix(Dictionary<IPEndPointProcessId, DistributedMatchUp.Match> ___m_remoteMatches, IPEndPointProcessId sender, DistributedMatchUp __instance)
        {
            Debug.Log(DateTime.Now.ToString() + " " + System.Diagnostics.Process.GetCurrentProcess().Id + " OnClientStateUpdate: sender " + sender + " all: " +
                ___m_remoteMatches.Join(x => x.Key + "[" + x.Value.uid + " " + x.Value.matchData.GetValueSafe("mm_ticketType") + " " + x.Value.matchData.GetValueSafe("mm_mmTickets") + "]"));
        }
    }

    [HarmonyPatch(typeof(DistributedMatchUp), "Overload.IBroadcastStateReceiver.OnClientDisconnect")]
    class LogDMUDisconnect
    {
        private static void Postfix(IPEndPointProcessId sender)
        {
            Debug.LogFormat("{0} {1} disconnect {2}", DateTime.Now.ToString(), System.Diagnostics.Process.GetCurrentProcess().Id, sender);
        }
    }

    // Log Client AddPlayer
    [HarmonyPatch(typeof(Client), "AddPlayer")]
    class LogClientAddPlayer
    {
        private static void Prefix()
        {
            UnityEngine.Networking.LogFilter.currentLogLevel = UnityEngine.Networking.LogFilter.Debug;
            Debug.Log("Client.AddPlayer");
        }
    }

    // Log scene loaded
    [HarmonyPatch(typeof(GameplayManager), "OnSceneLoaded")]
    class LogGPMOnSceneLoaded
    {
        private static void Postfix()
        {
            Debug.LogFormat("GameplayManager OnSceneLoaded GameplayManager.LevelIsLoaded={0}, DynamicGI.isConverged={1} GameManager.m_local_player.isLocalPlayer={2}",
                GameplayManager.LevelIsLoaded, DynamicGI.isConverged, GameManager.m_local_player.isLocalPlayer);
        }
    }
    #endif
}
