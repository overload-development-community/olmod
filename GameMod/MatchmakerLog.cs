using Harmony;
using Overload;

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
                message = string.Format(message, args).Replace("\r\n", " ");
                args = null;
            }
        }
    }

    /*
    // log FindGameSessionToCreate
    [HarmonyPatch(typeof(LocalLANHost), "FindGameSessionToCreate")]
    class MPMaxPlayerFindGameSessionToCreate
    {
        static void Prefix(DistributedMatchUp.Match[] requests, DistributedMatchUp.Match backfillSeedMatch)
        {
            foreach (var m in requests)
                Debug.Log(DateTime.Now.ToString() + ": HOST: FindGameSessionToCreate req " + m.uid + " mp=" + m.maxPlayers + " #p=" + JArray.Parse(m.matchData["mm_players"].stringValue).Count);
            if (backfillSeedMatch != null) {
                var m = backfillSeedMatch;
                Debug.Log(DateTime.Now.ToString() + ": HOST: FindGameSessionToCreate backfill " + m.uid + " mp=" + m.maxPlayers + " #p=" + JArray.Parse(m.matchData["mm_players"].stringValue).Count);
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
            Debug.Log(DateTime.Now.ToString() + " " + Process.GetCurrentProcess().Id + ": create match " + m.uid + " updated to " + m.matchData["mm_ticketType"]);
        }
    }

    // log UpdateMatch
    [HarmonyPatch(typeof(DistributedMatchUp), "UpdateMatch")]
    class MPMaxPlayerUpdateMatch
    {
        static void Postfix(DistributedMatchUp __instance)
        {
            DistributedMatchUp.Match m = __instance.ActiveMatch;
            Debug.Log(DateTime.Now.ToString() + " " + Process.GetCurrentProcess().Id + ": update match " + m.uid + " updated to " + m.matchData["mm_ticketType"]);
        }
    }

    // log received broadcast payloads for debugging
    [HarmonyPatch(typeof(DistributedMatchUp.Match), "Deserialize")]
    class MPMaxPlayerReceive
    {
        static void Postfix(DistributedMatchUp.Match __result)
        {
            var m = __result;
            Debug.Log(DateTime.Now.ToString() + " " + Process.GetCurrentProcess().Id + ": received " + (m == null ? "null" : m.uid + " type " + m.matchData["mm_ticketType"] + " tickets " + m.matchData.GetValueSafe("mm_mmTickets")));
        }
    }

    [HarmonyPatch(typeof(DistributedMatchUp), "Overload.IBroadcastStateReceiver.OnClientStateUpdate")]
    class MPMaxPlayerOnClientStateUpdate
    {
        static void Postfix(Dictionary<IPEndPointProcessId, DistributedMatchUp.Match> ___m_remoteMatches, IPEndPointProcessId sender)
        {
            Debug.Log(DateTime.Now.ToString() + " " + Process.GetCurrentProcess().Id + ": sender " + sender + " all: " + ___m_remoteMatches.Join());
        }
    }
    */
}
