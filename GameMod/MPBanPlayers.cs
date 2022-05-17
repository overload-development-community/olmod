using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod {
    // the set of ban modes we support
    public enum MPBanMode : int {
        Ban=0,
        Annoy,
        BlockChat,
        Count // End Marker, always add before
    }

    // Class for entries of the ban list
    // also used for candidate entries to check ban lists against
    public class MPBanEntry {
        public string name = null;
        public string address = null;
        public string id = null;
        public bool permanent = false;
        public DateTime timeCreated;

        public static string NullId = "00000000-0000-0000-0000-000000000000";

        // generate MPBanEntry from individual entries
        public MPBanEntry(string playerName, string connAddress, string playerId) {
            Set(playerName, connAddress, playerId);
        }

        // generate MPBanEntry from name, connection_id and id
        public MPBanEntry(string playerName, int connection_id, string playerId) {
            Set(playerName, connection_id, playerId);
        }

        // generate MPBanEntry from a Player
        public MPBanEntry(Player p) {
            if (p != null) {
                Set(p.m_mp_name, (p.connectionToClient != null)?p.connectionToClient.address:null, p.m_mp_player_id);
            }
        }

        // generate MPBanEntry from a PlayerLobbyData
        public MPBanEntry(PlayerLobbyData p) {
            if (p != null) {
                Set(p.m_name, p.m_id, p.m_player_id);
            }
        }

        // Set MPBanEntry from individual entries
        public void Set(string playerName, string connAddress, string playerId) {
            name = (String.IsNullOrEmpty(playerName))?null:playerName.ToUpper();
            address = (String.IsNullOrEmpty(connAddress))?null:connAddress.ToUpper().Trim();
            id = (String.IsNullOrEmpty(playerId))?null:playerId.ToUpper().Trim();
            if (id != null) {
                // check validity of the ID
                if (id.Length < 8 || id == NullId) {
                    Debug.LogFormat("BAN ENTRY: invalid player id {0}, ignoring", id);
                    id = null;
                }
            }
            timeCreated = DateTime.Now;
        }

        // Set MPBanEntry from name, connection_id, and id
        public void Set(string playerName, int connection_id, string playerId) {
            string addr=null;
            if (connection_id >= 0 && connection_id < NetworkServer.connections.Count && NetworkServer.connections[connection_id] != null) {
                addr = NetworkServer.connections[connection_id].address;
            } else {
                Debug.LogFormat("BAN ENTRY: failed to find connection for connection ID {0}", connection_id);
            }
            Set(playerName, addr, playerId);
        }

        // Set MPBanEntry from another entry
        public void Set(MPBanEntry other) {
            Set(other.name, other.address, other.id);
            timeCreated = other.timeCreated;
        }

        // Describe the entry as human-readable string
        public string Describe() {
            return String.Format("(name {0}, addr {1}, ID {2})", name, address, id);
        }

        // check if the entry matches some player
        public bool matches(MPBanEntry candidate, string prefix="") {
            /* name isn't a good criteria, so ignore it
            if (!String.IsNullOrEmpty(name) && !String.IsNullOrEmpty(candidate.name)) {
                if (name == candidate.name) {
                    Debug.LogFormat("{0}player {1} matches ban list entry {1} by NAME", prefix, candidate.Describe(), Describe());
                    return true;
                }

            }*/
            if (!String.IsNullOrEmpty(address) && !String.IsNullOrEmpty(candidate.address)) {
                if (address == candidate.address) {
                    Debug.LogFormat("{0}player {1} matches entry {2} by ADDRESS", prefix, candidate.Describe(), Describe());
                    return true;
                }
            }
            if (!String.IsNullOrEmpty(id) && !String.IsNullOrEmpty(candidate.id)) {
                if (id == candidate.id) {
                    Debug.LogFormat("{0}player {1} matches entry {2} by ID", prefix, candidate.Describe(), Describe());
                    return true;
                }
            }

            // does not match
            return false;
        }

        // Check wether a ban entry is valid
        public bool IsValid() {
            // we need at least id or address
            return !String.IsNullOrEmpty(id) || !String.IsNullOrEmpty(address);
        }
    }

    // class for managing banned players
    public class MPBanPlayers {
        // this is the Ban List
        private static List<MPBanEntry>[] banLists = new List<MPBanEntry>[(int)MPBanMode.Count];
        private static int totalBanCount=0; // Count of bans in all lists

        public static MPBanEntry MatchCreator = null; // description of the Game Creator
        public static MPBanEntry PlayerWithPrivateMatchData = null; // internal only: description of the player who's private match data was taken to create the game (id only)
        public static bool MatchCreatorIsInGame = false;
        public static string bannedName = " ** BANNED ** ";
        public static bool JoiningPlayerIsBanned = false;
        public static bool JoiningPlayerIsAnnoyed = false;
        public static int  JoiningPlayerConnectionId = -1;
        public static PlayerLobbyData JoiningPlayerLobbyData = null;

        // Get the ban list
        public static List<MPBanEntry> GetList(MPBanMode mode = MPBanMode.Ban) {
            int m = (int)mode;
            if (banLists[m] == null) {
                banLists[m] = new List<MPBanEntry>();
            }
            return banLists[m];
        }

        // Check if this player is banned
        public static bool IsBanned(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban) {
            var banList=GetList(mode);
            foreach (var entry in banList) {
                if (entry.matches(candidate, "BAN CHECK: ")) {
                    return true;
                }
            }
            // no ban entry matches this player..
            return false;
        }

        // Annoys a player
        public static void AnnoyPlayer(Player p)
        {
            if (p != null) {
                if (p.m_spectator == false) {
                    p.m_spectator = true;
                    Debug.LogFormat("BAN ANNOY player {0}",p.m_mp_name);
                    if (p.connectionToClient != null) {
                        MPChatTools.SendTo(String.Format("ANNOY-BANNING player {0}",p.m_mp_name), -1, p.connectionToClient.connectionId, true);
                    }
                }
            }
        }

        // Annoys all Players which are actively annoy-banned
        // Doesn't work in the lobby
        public static void AnnoyPlayers()
        {
            if (GetList(MPBanMode.Annoy).Count < 1) {
                // no bans active
                return;
            }
            foreach(var p in Overload.NetworkManager.m_Players) {
                MPBanEntry candidate = new MPBanEntry(p);
                if (IsBanned(candidate, MPBanMode.Annoy)) {
                    AnnoyPlayer(p);
                }
            }
        }

        // Delayed disconnect on Kick
        static private MethodInfo _Server_OnDisconnect_Method = AccessTools.Method(typeof(Overload.Server), "OnDisconnect");

        public static IEnumerator DelayedDisconnect(int connection_id, bool banned)
        {
            if (connection_id < NetworkServer.connections.Count && NetworkServer.connections[connection_id] != null) {
                NetworkConnection conn = NetworkServer.connections[connection_id];
                if (NetworkMatch.GetMatchState() >= MatchState.PREGAME) {
                    // Sending this command first prevents the client to load the scene
                    // and getting into some inconsistend state
                    NetworkServer.SendToClient(connection_id, CustomMsgType.MatchEnd, new IntegerMessage(0));
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                // nicely tell the client to FUCK OFF :)
                NetworkServer.SendToClient(connection_id, CustomMsgType.UnsupportedMatch, new StringMessage(String.Format("You {0} from this server",(banned)?"are BANNED":"were KICKED")));
                yield return new WaitForSecondsRealtime(0.5f);
                // Fake client's OnDisconnect message
                NetworkMessage msg = new NetworkMessage();
                msg.conn = conn;
                msg.msgType = 33; // Disconnect
                _Server_OnDisconnect_Method.Invoke(null, new object[] { msg });
                // get rid of the client
                conn.Disconnect();
            }
        }

        // Kicks a player by a connection id. Also works in the Lobby
        public static void KickPlayer(int connection_id, string name="", bool banned = false)
        {
            if (connection_id < 0 || connection_id >= NetworkServer.connections.Count) {
                return;
            }
            if (NetworkServer.connections[connection_id] != null) {
                MPChatTools.SendTo(String.Format("KICKING {0}player {1}",((banned)?"BANNED ":""),name), -1, connection_id, true);
                // Goodbye
                // disconnect it in short moment, to give client time to execute the commands
                GameManager.m_gm.StartCoroutine(DelayedDisconnect(connection_id,banned));
            }

        }

        // Kicks an active Player
        public static void KickPlayer(Player p, bool banned = false)
        {
            if (p != null && p.connectionToClient != null) {
                KickPlayer(p.connectionToClient.connectionId, p.m_mp_name, banned);
            }
        }

        // Kicks an Player in Lobby State
        public static void KickPlayer(PlayerLobbyData p, bool banned = false)
        {
            if (p != null) {
                KickPlayer(p.m_id, p.m_name, banned);
            }
        }

        // Kicks all players who are BANNED (Lobby and otherwise)
        public static void KickBannedPlayers()
        {
            if (GetList(MPBanMode.Ban).Count < 1) {
                // no bans active
                return;
            }
            MatchState s = NetworkMatch.GetMatchState();
            bool inLobby = (s == MatchState.LOBBY || s == MatchState.LOBBY_LOAD_COUNTDOWN);
            if (inLobby) {
                // Kick Lobby Players
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    if (p.Value != null) {
                        MPBanEntry candidate = new MPBanEntry(p.Value);
                        if (IsBanned(candidate, MPBanMode.Ban)) {
                            KickPlayer(p.Value, true);
                        }
                    }
                }
            } else {
                foreach(var p in Overload.NetworkManager.m_Players) {
                    MPBanEntry candidate = new MPBanEntry(p);
                    if (IsBanned(candidate, MPBanMode.Ban)) {
                        KickPlayer(p, true);
                    }
                }
            }
        }

        // Appy all bans to all all active players in Game
        public static void ApplyAllBans()
        {
            if (totalBanCount < 1) {
                return;
            }
            AnnoyPlayers();
            KickBannedPlayers();
        }

        // The ban list was modified
        public static void OnUpdate(MPBanMode mode, bool added)
        {
            totalBanCount = 0;
            for (MPBanMode m = (MPBanMode)0; m < MPBanMode.Count; m++) {
                totalBanCount += GetList(m).Count;
            }
            if (added) {
                if (mode == MPBanMode.Annoy) {
                    // apply Annoy bans directly, but not normal bans, as we have the specil KICK and KICKBAN commands
                    // that way, we can ban players without having them to be immediately kicked
                    AnnoyPlayers();
                }
            }
        }

        // Add a player to the Ban List
        public static bool Ban(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban, bool permanent = false)
        {
            if (!candidate.IsValid()) {
                return false;
            }
            candidate.permanent = permanent;
            var banList=GetList(mode);
            foreach (var entry in banList) {
                if (entry.matches(candidate, "BAN already matched: ")) {
                    // Update it
                    entry.Set(candidate);
                    OnUpdate(mode, true);
                    return false;
                }
            }
            banList.Add(candidate);
            Debug.LogFormat("BAN: player {0} is NOW banned in mode: {1}", candidate.Describe(), mode);
            OnUpdate(mode, true);
            return true;
        }

        // Remove all entries matching a candidate from the ban list
        public static int Unban(MPBanEntry candidate, MPBanMode mode = MPBanMode.Ban)
        {
            var banList = GetList(mode);
            int cnt = banList.RemoveAll(entry => entry.matches(candidate,"UNBAN: "));
            if (cnt > 0) {
                OnUpdate(mode, false);
            }
            return cnt;
        }

        // Remove all bans
        public static void UnbanAll(MPBanMode mode = MPBanMode.Ban) {
            var banList = GetList(mode);
            banList.Clear();
            OnUpdate(mode, false);
        }

        // Remove all non-Permanent bans
        public static void UnbanAllNonPermanent(MPBanMode mode = MPBanMode.Ban) {
            var banList = GetList(mode);
            banList.RemoveAll(entry => entry.permanent == false);
            OnUpdate(mode, false);
        }

        // Check if the MPBanPlayers has a non-default state
        // This checks if any bans are present
        public static bool HasModifiedState() {
            return (totalBanCount > 0);
        }

        // Reset the MPBanPlayers state
        // Removes all non-permanent bans of all modes
        public static void Reset() {
            Debug.Log("MPBanPlayers: resetting all non-permanent bans");
            for (MPBanMode mode = (MPBanMode)0; mode < MPBanMode.Count; mode++) {
                UnbanAllNonPermanent(mode);
            }
        }
    }

    // A new match is created, clear the state
    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MPBanPlayers_InitBeforeEachMatch {
        private static void Postfix() {
            MPBanPlayers.MatchCreatorIsInGame = false;
        }
    }

    // Find the player ID of the player who created the game
    [HarmonyPatch(typeof(NetworkMatch), "NetSystemOnGameSessionStart")]
    class MPBanPlayers_FindGameCreator
    {
        static void SetCreatorId(string id) {
            MPBanPlayers.PlayerWithPrivateMatchData = new MPBanEntry(null, null, id);
            //Debug.LogFormat("MPBanPlayers: FindGameCreator: player with private match data is id {0}",id);
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            var our_Method = AccessTools.Method(typeof(MPBanPlayers_FindGameCreator), "SetCreatorId");
            LocalBuilder hpmi = null; // the Local variable storing the HostPlayerMatchmakerInfo
            Type hpmiType = null;
            int state = 0;

            foreach (var code in codes)
            {
                //Debug.LogFormat("YYTS: {0} {1} {2}",state,code.opcode,code.operand);
                if (state == 0) {
                    /// Search the part where the private match data is searched
                    if (code.opcode == OpCodes.Ldstr && (string)code.operand == "Looking for player with private match data...") {
                        state = 1;
                    }
                } else if (state == 1) {
                    // the latest Stloc_S before we look up the private_match_data field is the one we search for
                    if (code.opcode == OpCodes.Stloc_S) {
                        hpmi = (LocalBuilder)code.operand;
                        state = 2;
                    } else if (code.opcode == OpCodes.Ldstr && (string)code.operand == "private_match_data") {
                        state = -1;
                    }
                } else if (state == 2) {
                    // check if Stloc.s hpmi is immediately followed by Ldloc.s hpmi
                    if (code.opcode == OpCodes.Ldloc_S && (LocalBuilder)code.operand == hpmi) {
                        state = 3;
                    } else {
                        // not the one we're searching for
                        state = 1;
                    }
                } else if (state == 3) {
                    // check if immediately followed by Ldfld m_player
                    if (code.opcode == OpCodes.Ldfld) {
                        FieldInfo field=(FieldInfo)code.operand;
                        if (field.Name == "m_player") {
                            hpmiType = field.DeclaringType;
                            state = 4;
                        } else {
                            // not the one we're searching for
                            state = 1;
                        }
                    } else {
                        // not the one we're searching for
                        state = 1;
                    }
                } else if (state == 4) {
                    if (code.opcode == OpCodes.Ldstr && (string)code.operand == "Found private match data: {0}") {
                        state = 5;
                        // Actual patch:
                        // Load field hpmi.m_playerId onto stack
                        var hpmiPlayerID = AccessTools.Field(hpmiType, "m_playerId");
                        yield return new CodeInstruction(OpCodes.Ldloc_S, hpmi);
                        yield return new CodeInstruction(OpCodes.Ldfld, hpmiPlayerID);
                        // feed it to our method
                        yield return new CodeInstruction(OpCodes.Call, our_Method);
                    }
                }
                yield return code;
            }
            if (state != 5) {
                Debug.LogFormat("MPBanPlayers_FindGameCreator: transpiler failed at state {0}",state);
            }
        }
    }

    // Check the player joining the lobby,
    // Apply bans  and annoy-bans
    // Also check if the joining player is the match creator,
    // and reset bans and permissions accordingly for the new match.
    [HarmonyPatch(typeof(NetworkMatch), "AcceptNewConnection")]
    class MPBanPlayers_AcceptNewConnection {
        // Delayed end game
        public static IEnumerator DelayedEndMatch()
        {
            yield return new WaitForSecondsRealtime(3);
            NetworkMatch.End();
        }

        private static void Postfix(ref bool __result, int connection_id, int version, string player_name, string player_session_id, string player_id, string player_uid) {
            MPBanPlayers.JoiningPlayerIsBanned = false;
            MPBanPlayers.JoiningPlayerIsAnnoyed = false;
            MPBanPlayers.JoiningPlayerConnectionId = connection_id;
            MPBanPlayers.JoiningPlayerLobbyData = null;
            if (__result) {
                // the player has been accepted by the game's matchmaking
                MPBanEntry candidate = new MPBanEntry(player_name, connection_id, player_id);
                bool isCreator = false;
                if (!MPBanPlayers.MatchCreatorIsInGame && MPBanPlayers.PlayerWithPrivateMatchData != null && !String.IsNullOrEmpty(MPBanPlayers.PlayerWithPrivateMatchData.id) && !String.IsNullOrEmpty(candidate.id)) {
                    if (candidate.id == MPBanPlayers.PlayerWithPrivateMatchData.id) {
                        Debug.LogFormat("MPBanPlayers: Match creator entered the lobby: {0}",player_name);
                        isCreator = true;
                        MPBanPlayers.MatchCreatorIsInGame = true;
                    }
                }
                // check if player is banned
                MPBanPlayers.JoiningPlayerIsBanned = MPBanPlayers.IsBanned(candidate);
                MPBanPlayers.JoiningPlayerIsAnnoyed = MPBanPlayers.IsBanned(candidate, MPBanMode.Annoy);
                if (isCreator && MPBanPlayers.JoiningPlayerIsAnnoyed) {
                    // annoyed players are treated as Banned for creating new matches
                    MPBanPlayers.JoiningPlayerIsBanned = true;
                }
                if (MPBanPlayers.JoiningPlayerIsBanned) {
                    // banned player entered the lobby
                    // NOTE: we cannot just say __accept = false, because this causes all sorts of troubles later
                    MPBanPlayers.KickPlayer(connection_id, player_name, true);
                    if (isCreator) {
                        Debug.LogFormat("Creator for this match {0} is BANNED, ending match", candidate.name);
                        GameManager.m_gm.StartCoroutine(DelayedEndMatch());
                    }
                } else {
                    // unbanned player entered the lobby
                    if (isCreator) {
                        bool haveModifiedState = MPBanPlayers.HasModifiedState() || MPChatCommand.HasModifiedState();
                        bool doReset = true;
                        if (MPChatCommand.CheckPermission(candidate)) {
                            Debug.Log("MPBanPlayers: same game creator as last match, or with permissions");
                            doReset = false;
                        }
                        MPBanPlayers.MatchCreator = candidate;
                        if (doReset) {
                            Debug.Log("MPBanPlayers: new game creator, resetting bans and permissions");
                            MPBanPlayers.Reset();
                            MPChatCommand.Reset();
                            if (haveModifiedState) {
                                MPChatTools.SendTo(true, "cleared all bans and permissions", connection_id);
                            }
                        } else {
                            if (haveModifiedState) {
                                MPChatTools.SendTo(true, "keeping bans and permissions from previous match", connection_id);
                            }
                        }
                    }
                }
            }
        }
    }

    // Don't send banned players in the lobby to other players
    // rename ANNOY-BANNED players
    [HarmonyPatch(typeof(Overload.Server), "SendPlayersInLobbyToAllClients")]
    class MPBanPlayers_SendPlayersInLobbyToAllClients {
        private static void Prefix() {
            if (MPBanPlayers.JoiningPlayerConnectionId >= 0) {
                if (MPBanPlayers.JoiningPlayerIsAnnoyed) {
                    // rename annoy-banned players
                    if (MPBanPlayers.JoiningPlayerIsAnnoyed) {
                        foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                            if (p.Key == MPBanPlayers.JoiningPlayerConnectionId) {
                                if (p.Value != null) {
                                    MPChatTools.SendTo(String.Format("ANNOY-BANNED player {0} joined, renamed to {1}",p.Value.m_name, MPBanPlayers.bannedName), -1, MPBanPlayers.JoiningPlayerConnectionId, true);
                                    p.Value.m_name = MPBanPlayers.bannedName;
                                }
                            }
                        }
                    }
                }
                if (MPBanPlayers.JoiningPlayerIsBanned) {
                    if (NetworkMatch.m_players.ContainsKey(MPBanPlayers.JoiningPlayerConnectionId)) {
                        // Save the PlayerLobbyData that we remove here, we add it back after SendPlayersInLobbyToAllClients
                        MPBanPlayers.JoiningPlayerLobbyData = NetworkMatch.m_players[MPBanPlayers.JoiningPlayerConnectionId];
                        NetworkMatch.m_players.Remove(MPBanPlayers.JoiningPlayerConnectionId);
                    }
                }
            }
        }

        private static void Posfix() {
            if (MPBanPlayers.JoiningPlayerIsBanned && MPBanPlayers.JoiningPlayerConnectionId >= 0 && MPBanPlayers.JoiningPlayerLobbyData != null) {
                // Add back the PlayerLobbyData which we temporarily removed
                NetworkMatch.m_players.Add(MPBanPlayers.JoiningPlayerConnectionId, MPBanPlayers.JoiningPlayerLobbyData);
            }
            // clear the JoiningPlayer ban state, everything is applied
            MPBanPlayers.JoiningPlayerIsBanned = false;
            MPBanPlayers.JoiningPlayerIsAnnoyed = false;
            MPBanPlayers.JoiningPlayerConnectionId = -1;
            MPBanPlayers.JoiningPlayerLobbyData = null;
        }
    }

    // Apply the bans when the match starts
    [HarmonyPatch(typeof(NetworkMatch), "StartPlaying")]
    class MPBanPlayers_OnStartPlaying {
        private static void Postfix() {
            MPBanPlayers.ApplyAllBans();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "SetPlayerId")]
    class MPChatCommands_SetPlayerId {
        public static bool StartMatch = false;
        private static void Prefix(ref string player_id) {
            // the game uses an AWS web service to query the player IDs
            // If it is down or not reachable, we get all zero ID,
            // fall back to the local user ID from the "prefs" file
            if (String.IsNullOrEmpty(player_id) || player_id == MPBanEntry.NullId) {
                string id = PlayerPrefs.GetString("UserID");
                if (String.IsNullOrEmpty(id)) {
                    id = Guid.NewGuid().ToString();
                    PlayerPrefs.SetString("UserID", id);
                    PlayerPrefs.Save();
                }
                player_id = id;
            }
        }
    }
}
