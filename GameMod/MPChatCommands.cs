using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    // Generic utility class for chat messages
    public class MPChatTools {

        // helper function to send a lobby chat message
        private static void SendToLobbyHelper(LobbyChatMessage lmsg, int connection_id, bool authOnly, bool unblockedOnly) {
            bool doSend = true;
            if (authOnly || unblockedOnly) {
                MPBanEntry entry = MPChatCommand.FindPlayerEntryForConnection(connection_id, true);
                if (authOnly) {
                    doSend = doSend && MPChatCommand.CheckPermission(entry, connection_id);
                }
                if (doSend && unblockedOnly) {
                    if (entry != null) {
                        doSend = !MPBanPlayers.IsBanned(entry, MPBanMode.BlockChat);
                    } else {
                        doSend = false;
                    }
                }
            }
            if (doSend) {
                NetworkServer.SendToClient(connection_id, 75, lmsg);
            }
        }

        // Send a chat message
        // Set connection_id to the ID of a specific client, or to -1 to send to all
        // clients except except_connection_id (if that is >= 0)
        // If authOnly is true, only send to clients which are authenticated for chat
        // commands.
        // If unblockedOnly is true, only send to clients which are not BlockChat-BANNED
        // You need to already know if we are currently in Lobby or not
        public static bool SendTo(bool inLobby, string msg, int connection_id=-1, int except_connection_id=-1, bool authOnly=false, bool unblockedOnly=false, string sender_name = "Server", MpTeam team = MpTeam.TEAM0, bool isTeamMessage=false, int sender_connection_id=-1) {
            bool toAll = (connection_id < 0);
            if (!toAll) {
                if (connection_id >= NetworkServer.connections.Count ||  NetworkServer.connections[connection_id] == null) {
                    return false;
                }
            }

            if (inLobby) {
                var lmsg = new LobbyChatMessage(sender_connection_id, sender_name, team, msg, isTeamMessage);
                if (toAll) {
                    if (except_connection_id < 0 && !authOnly && !unblockedOnly) {
                        NetworkServer.SendToAll(75, lmsg);
                    } else {
                        foreach(NetworkConnection c in NetworkServer.connections) {
                            if (c != null && c.connectionId != except_connection_id) {
                                SendToLobbyHelper(lmsg, c.connectionId, authOnly, unblockedOnly);
                            }
                        }
                    }
                } else {
                    SendToLobbyHelper(lmsg, connection_id, authOnly, unblockedOnly);
                }
            } else {
                if (isTeamMessage) {
                    msg = Loc.LS("[TEAM]")+" " + msg;
                }
                foreach (var p in Overload.NetworkManager.m_Players) {
                    if (p != null && p.connectionToClient != null) {
                        if ((toAll && p.connectionToClient.connectionId != except_connection_id) || (p.connectionToClient.connectionId == connection_id)) {
                            bool doSend = true;
                            if (authOnly || unblockedOnly) {
                                MPBanEntry entry = new MPBanEntry(p);
                                if (authOnly) {
                                    doSend = doSend && MPChatCommand.CheckPermission(entry, p.connectionToClient.connectionId);
                                }
                                if (doSend && unblockedOnly) {
                                    doSend = !MPBanPlayers.IsBanned(entry, MPBanMode.BlockChat);
                                }
                            }
                            if (doSend) {
                                p.CallTargetSendFullChat(p.connectionToClient, connection_id, sender_name, team, msg);
                            }
                        }
                    }
                }
            }
            return true;
        }

        // Send a chat message
        // Set connection_id to the ID of a specific client, or to -1 to send to all
        // clients except except_connection_id (if that is >= 0)
        // if onlyAuth is true, onlsy sent to clients which are authenticated for
        // If unblockedOnly is true, only send to clients which are not BlockChat-BANNED
        // chat commands
        public static bool SendTo(string msg, int connection_id=-1 , int except_connection_id=-1, bool authOnly=false, bool unblockedOnly=false, string sender_name = "Server", MpTeam team = MpTeam.TEAM0, bool isTeamMessage=false, int sender_connection_id=-1) {
            MatchState s = NetworkMatch.GetMatchState();
            if (s == MatchState.NONE || s == MatchState.LOBBY_LOADING_SCENE) {
                Debug.LogFormat("MPChatTools SendTo() called during match state {0}, ignored",s);
                return false;
            }
            bool inLobby = (s == MatchState.LOBBY || s == MatchState.LOBBY_LOAD_COUNTDOWN);
            return SendTo(inLobby, msg, connection_id, except_connection_id, authOnly, unblockedOnly, sender_name, team, isTeamMessage, sender_connection_id);
        }
    }

    // Class dealing with special chat commands
    public class MPChatCommand {
        // Enumeration of all defined Commands
        public enum Command {
            None, // not a known command
            // List of Commands
            GivePerm,
            RevokePerm,
            Auth,
            Kick,
            Ban,
            KickBan,
            Unban,
            Annoy,
            Unannoy,
            BlockChat,
            UnblockChat,
            End,
            Start,
            Extend,
            Status,
            Say,
            Test,
            SwitchTeam,
            ListPlayers,
        }

        // properties:
        Command cmd;
        public string cmdName;
        public string arg;
        public int sender_conn;
        public string sender_name;
        public MpTeam sender_team;
        public string sender_message;
        public bool isTeamMessage;
        public bool needAuth;
        public bool inLobby;
        public MPBanEntry senderEntry = null;
        public MPBanEntry selectedPlayerEntry = null;
        public Player selectedPlayer = null;
        public PlayerLobbyData selectedPlayerLobbyData = null;
        public int selectedPlayerConnectionId = -1;

        // this Dictionary contains the set of authenticated players
        // Authentication is done based on Player.m_player_id / PlayerLobbyData.m_player_id
        private static List<MPBanEntry> authenticatedPlayers = new List<MPBanEntry>();
        private static List<MPBanEntry> trustedPlayers = null;
        private static int chatCommandsEnabled = -1;

        // Check if chat commands are enabled.
        public static bool IsEnabled()
        {
            if (chatCommandsEnabled < 0) {
                chatCommandsEnabled = GameMod.Core.GameMod.FindArg("-disableChatCommands")?0:1;
            }
            return (chatCommandsEnabled != 0);
        }

        // Construct a MPChatCommand from a Chat message
        public MPChatCommand(string message, int sender_connection_id, string sender_player_name, MpTeam sender_player_team, bool isInLobby) {
            cmd = Command.None;

            isTeamMessage = NetworkMessageManager.IsTeamMessage(message);
            if (isTeamMessage) {
                message = NetworkMessageManager.StripTeamPrefix(message);
            }

            sender_conn = sender_connection_id;
            sender_name = sender_player_name;
            sender_team = sender_player_team;
            sender_message = message;
            needAuth = false;
            inLobby = isInLobby;
            senderEntry = null;

            if (message == null || message.Length < 2 || message[0] != '/') {
                // not a valid command
                return;
            }

            // is there an additonal argument to this command?
            // Arguments are separated with space
            int split = message.IndexOf(' ');
            if (split > 0) {
                cmdName = message.Substring(1,split-1);
                if (split + 1 < message.Length) {
                    arg = message.Substring(split+1, message.Length - split -1);
                }
            } else {
                cmdName = message.Substring(1,message.Length - 1);
            }

            // detect the command
            cmdName = cmdName.ToUpper();
            if (cmdName == "GP" || cmdName == "GIVEPERM") {
                cmd = Command.GivePerm;
                needAuth = true;
            } else if (cmdName == "RP" || cmdName == "REVOKEPERM") {
                cmd = Command.RevokePerm;
                needAuth = true;
            } else if (cmdName == "AUTH") {
                cmd = Command.Auth;
                needAuth = false;
            } else if (cmdName == "K" || cmdName == "KICK") {
                cmd = Command.Kick;
                needAuth = true;
            } else if (cmdName == "B" || cmdName == "BAN") {
                cmd = Command.Ban;
                needAuth = true;
            } else if (cmdName == "A" || cmdName == "ANNOY") {
                cmd = Command.Annoy;
                needAuth = true;
            } else if (cmdName == "UA" || cmdName == "UNANNOY") {
                cmd = Command.Unannoy;
                needAuth = true;
            } else if (cmdName == "KB" || cmdName == "KICKBAN") {
                cmd = Command.KickBan;
                needAuth = true;
            } else if (cmdName == "UB" || cmdName == "UNBAN") {
                cmd = Command.Unban;
                needAuth = true;
            } else if (cmdName == "BC" || cmdName == "BLOCKCHAT") {
                cmd = Command.BlockChat;
                needAuth = true;
            } else if (cmdName == "UBC" || cmdName == "UNBLOCKCHAT") {
                cmd = Command.UnblockChat;
                needAuth = true;
            } else if (cmdName == "E" || cmdName == "END") {
                cmd = Command.End;
                needAuth = true;
            } else if (cmdName == "S" || cmdName == "START") {
                cmd = Command.Start;
                needAuth = true;
            } else if (cmdName == "EX" || cmdName == "EXTEND") {
                cmd = Command.Extend;
                needAuth = true;
            } else if (cmdName == "I" || cmdName == "INFO" || cmdName == "STATUS") {
                cmd = Command.Status;
            } else if (cmdName == "SAY" || cmdName == "CHAT") {
                cmd = Command.Say;
            } else if (cmdName == "T" || cmdName == "TEST") {
                cmd = Command.Test;
            } else if (cmdName == "ST" || cmdName == "SWITCHTEAM") {
                cmd = Command.SwitchTeam;
            } else if (cmdName == "LP" || cmdName == "LISTPLAYERS") {
                cmd = Command.ListPlayers;
            }
        }

        // Execute a command: Returns true if the caller should forward the chat message
        // to the clients, and false if not (when it was a special command for the server)
        public bool Execute() {
            if (!IsEnabled()) {
                // chat commands are not enabled
                return true;
            }

            if (cmd == Command.None) {
                // there might be BlockChat-Banned players, and we can just ignore their ramblings
                if (MPBanPlayers.GetList(MPBanMode.BlockChat).Count > 0) {
                    MPBanEntry we = FindPlayerEntryForConnection(sender_conn, inLobby);
                    if (we != null && MPBanPlayers.IsBanned(we, MPBanMode.BlockChat)) {
                        // send the message back to the sender ONLY
                        MPChatTools.SendTo(inLobby, sender_message, sender_conn, -1, false, false, sender_name, sender_team, isTeamMessage, sender_conn);
                        return false;
                    }
                }
                return true;
            }
            senderEntry=FindPlayerEntryForConnection(sender_conn, inLobby);
            if (senderEntry == null || String.IsNullOrEmpty(senderEntry.name)) {
                Debug.LogFormat("CHATCMD {0}: {1} {2}: failed to identify sender", cmd, cmdName, arg);
                return false;
            }
            Debug.LogFormat("CHATCMD {0}: {1} {2} by {3}", cmd, cmdName, arg, senderEntry.name);
            if (needAuth) {
                if (!CheckPermission(senderEntry,sender_conn)) {
                    ReturnToSender(String.Format("You do not have the permission for command {0}!", cmd));
                    Debug.LogFormat("CHATCMD {0}: client is not authenticated!", cmd);
                    return false;
                }
            }
            bool result = false;
            switch (cmd) {
                case Command.GivePerm:
                    result = DoPerm(true);
                    break;
                case Command.RevokePerm:
                    result = DoPerm(false);
                    break;
                case Command.Auth:
                    result = DoAuth();
                    break;
                case Command.Kick:
                    result = DoKickBan(true, false, MPBanMode.Ban);
                    break;
                case Command.Ban:
                    result = DoKickBan(false, true, MPBanMode.Ban);
                    break;
                case Command.Annoy:
                    result = DoKickBan(false, true, MPBanMode.Annoy);
                    break;
                case Command.KickBan:
                    result = DoKickBan(true, true, MPBanMode.Ban);
                    break;
                case Command.Unban:
                    result = DoUnban(MPBanMode.Ban);
                    break;
                case Command.Unannoy:
                    result = DoUnban(MPBanMode.Annoy);
                    break;
                case Command.BlockChat:
                    result = DoKickBan(false, true, MPBanMode.BlockChat);
                    break;
                case Command.UnblockChat:
                    result = DoUnban(MPBanMode.BlockChat);
                    break;
                case Command.End:
                    result = DoEnd();
                    break;
                case Command.Start:
                    result = DoStartExtend(true);
                    break;
                case Command.Extend:
                    result = DoStartExtend(false);
                    break;
                case Command.Status:
                    result = DoStatus();
                    break;
                case Command.Say:
                    result = DoSay();
                    break;
                case Command.Test:
                    result = DoTest();
                    break;
                case Command.SwitchTeam:
                    result = DoSwitchTeam();
                    break;
                case Command.ListPlayers:
                    result = DoListPlayers();
                    break;
                default:
                    Debug.LogFormat("CHATCMD {0}: {1} {2} was not handled by server", cmd, cmdName, arg);
                    result = true; // treat it as normal chat message
                    break;
            }
            return result;
        }

        // get the trusted player IDs from the commandline
        private static void GetTrustedPlayerIds() {
            if (trustedPlayers != null) {
                return; // already set
            }
            trustedPlayers = new List<MPBanEntry>();
            string idstring = null;
            if (!GameMod.Core.GameMod.FindArgVal("-trustedPlayerIds", out idstring) || String.IsNullOrEmpty(idstring)) {
                return; // no trustedPlayerIds specified;
            }
            string[] ids = idstring.Split(',',';',':','|');
            foreach (string id in ids) {
                MPBanEntry entry = new MPBanEntry(null, null, id);
                if (entry.IsValid()) {
                    bool doAdd = true;
                    foreach(MPBanEntry e in trustedPlayers) {
                        if (e.matches(entry, "TRUSTED PLAYER: ")) {
                            doAdd = false;
                        }
                    }
                    if (doAdd) {
                        Debug.LogFormat("MPChatCommands: adding trusted player {0}", entry.Describe());
                        trustedPlayers.Add(entry);
                    }
                }
            }
        }

        // set authentication status of player by id
        private static bool SetAuth(bool allowed, MPBanEntry playerEntry)
        {
            if (playerEntry == null || !playerEntry.IsValid()) {
                Debug.LogFormat("SETAUTH called without valid player");
                return false;
            }

            if (allowed) {
                Debug.LogFormat("AUTH: player {0} is authenticated", playerEntry.name);
                bool doAdd = true;
                foreach(MPBanEntry entry in authenticatedPlayers) {
                    if (entry.matches(playerEntry, "AUTHENTICATED PLAYER: ")) {
                        entry.Set(playerEntry);
                        doAdd = false;
                    }
                }
                if (doAdd) {
                    authenticatedPlayers.Add(playerEntry);
                }
            } else {
                // de-auth
                Debug.LogFormat("AUTH: player {0} is NOT authenticated any more", playerEntry.name);
                if (authenticatedPlayers.RemoveAll(entry => entry.matches(playerEntry, "AUTHENTICATED PLAYER: ")) < 1) {
                    return false;
                }
            }
            return true;
        }

        // check if the supplied password matches the server password
        private static bool SetAuthIsPassword(string password)
        {
            if (String.IsNullOrEmpty(password)) {
                Debug.Log("SETAUTH Password check called without password");
                return false;
            }
            string serverPassword = null;
            if (!GameMod.Core.GameMod.FindArgVal("-chatCommandPassword", out serverPassword) || String.IsNullOrEmpty(serverPassword)) {
                Debug.Log("SETAUTH Password is DISABLED on this server");
                return false;
            }
            if (password.ToUpper() == serverPassword.ToUpper()) {
                Debug.Log("SETAUTH Password CORRECT");
                return true;
            }
            Debug.LogFormat("SETAUTH Password {0} is WRONG", password);
            return false;
        }

        // Check if a player is an authenticated player on this server
        public static bool IsAuthenticatedPlayer(MPBanEntry playerEntry)
        {
            if (playerEntry == null || !playerEntry.IsValid()) {
                Debug.LogFormat("IsAuthenticatedPlayer without valid player");
                return false;
            }
            foreach(MPBanEntry e in authenticatedPlayers) {
                if (e.matchesStrict(playerEntry, "AUTHENTICATED PLAYER: ")) {
                    return true;
                }
            }
            return false;
        }

        // Check if a player is a trusted player on this server
        public static bool IsTrustedPlayer(MPBanEntry entry)
        {
            if (entry == null || String.IsNullOrEmpty(entry.id)) {
                return false;
            }
            GetTrustedPlayerIds();
            foreach(MPBanEntry e in trustedPlayers) {
                if (e.matches(entry, "TRUSTED PLAYER: ")) {
                    Debug.LogFormat("MPChatCommands: player {0} id {0} is trusted on this server", entry.name, entry.id);
                    return true;
                }
            }
            return false;
        }

        // Execute GIVEPERM/REVOKEPERM command
        public bool DoPerm(bool give)
        {
            string op = (give)?"GIVEPERM":"REVOKEPERM";
            if (!SelectPlayer(arg)) {
                if (give) {
                    Debug.LogFormat("{0}: no player {1} found", op, arg);
                    ReturnToSender(String.Format("{0}: player {1} not found",op, arg));
                    return false;
                } else {
                    authenticatedPlayers.Clear();
                    Debug.LogFormat("{0}: all client permissions revoked", op, arg);
                    ReturnToSender(String.Format("{0}: all client permissions revoked",op));
                }
            }

            if (SetAuth(give, selectedPlayerEntry)) {
                ReturnToSender(String.Format("{0}: player {1} applied",op,selectedPlayerEntry.name));
                if (selectedPlayerConnectionId >= 0) {
                    ReturnTo(String.Format("{0} COMMAND permission by {1}",((give)?"Granted":"Revoked"), senderEntry.name),selectedPlayerConnectionId);
                }
            } else {
                ReturnToSender(String.Format("{0}: player {1} failed",op,selectedPlayerEntry.name));
            }

            return false;
        }

        // Execute the AUTH command
        public bool DoAuth()
        {
            if (!SetAuthIsPassword(arg)) {
                ReturnToSender("Nope.");
                // De-Authenticate player with wrong password
                SetAuth(false, senderEntry);
                return false;
            }

            if (IsAuthenticatedPlayer(senderEntry)) {
                ReturnToSender("Already Authenticated");
                return false;
            }
            if (SetAuth(true, senderEntry)) {
                ReturnToSender("AUTH successfull.");
            } else {
                // this shouldn't really happen...
                ReturnToSender("AUTH failed! See server log for details.");
            }
            return false;
        }

        // Execute KICK or BAN or KICKBAN or ANNOY or BLOCKCHAT command
        public bool DoKickBan(bool doKick, bool doBan, MPBanMode banMode) {
            string op;
            string banOp = banMode.ToString().ToUpper();
            if (doKick && doBan) {
                op = "KICK" + banOp;
            } else if (doKick) {
                op = "KICK";
            } else if (doBan) {
                op = banOp;
            } else {
                return false;
            }

            Debug.LogFormat("{0} request for {1}", op, arg);
            if (!SelectPlayer(arg)) {
                Debug.LogFormat("{0}: no player {1} found", op, arg);
                ReturnToSender(String.Format("{0}: player {1} not found",op, arg));
                return false;
            }

            if (IsTrustedPlayer(selectedPlayerEntry)) {
                ReturnToSender(String.Format("{0}: not on this server, dude!",op));
                return false;
            }

            if(selectedPlayerConnectionId >= 0) {
                if (sender_conn == selectedPlayerConnectionId) {
                    Debug.LogFormat("{0}: won't self-apply", op);
                    ReturnToSender(String.Format("{0}: won't self-apply",op));
                    return false;
                }
                if (selectedPlayerConnectionId == MPBanPlayers.MatchCreatorConnectionId) {
                    Debug.LogFormat("{0}: won't apply to match creator", op);
                    ReturnToSender(String.Format("{0}: won't apply to match creator",op));
                    return false;
                }
            }

            if (doBan) {
                if (!selectedPlayerEntry.IsValid()) {
                    Debug.LogFormat("{0}: failed to find ban entry for player {1}", op, arg);
                    ReturnToSender(String.Format("{0}: failed to find ban entry for {1}",op, arg));
                    return false;
                }
                // Make sure the newly created ban entry won't alias the match creator or 
                // any other currently authenticated player. 
                if (!TrimBanCandidate(ref selectedPlayerEntry)) {
                    // we can't ban the player as it would completely alias us
                    return false;
                }
                
                MPBanPlayers.Ban(selectedPlayerEntry, banMode);
                if (banMode == MPBanMode.Annoy) {
                    // ANNOY also implies BLOCKCHAT
                    MPBanPlayers.Ban(selectedPlayerEntry, MPBanMode.BlockChat);
                }
                ReturnTo(String.Format("{0} player {1} by {2}", banOp, selectedPlayerEntry.name, senderEntry.name), -1, selectedPlayerConnectionId);
            }
            if (doKick) {
                ReturnTo(String.Format("KICK player {0} by {1}", selectedPlayerEntry.name, senderEntry.name), -1, selectedPlayerConnectionId);
                if (selectedPlayer != null) {
                    MPBanPlayers.KickPlayer(selectedPlayer);
                } else if (selectedPlayerLobbyData != null) {
                    MPBanPlayers.KickPlayer(selectedPlayerLobbyData);
                } else {
                    MPBanPlayers.KickPlayer(selectedPlayerConnectionId, selectedPlayerEntry.name);
                }
            }
            return false;
        }

        // Execute UNBAN or UNANNOY or UNBLOCKCHAT
        public bool DoUnban(MPBanMode banMode)
        {
            if (banMode == MPBanMode.Annoy) {
                // UNANNOY also implies UNBLOCKCHAT
                DoUnban(MPBanMode.BlockChat);
            }
            if (String.IsNullOrEmpty(arg)) {
                MPBanPlayers.UnbanAll(banMode);
                ReturnTo(String.Format("ban list {0} cleared by {1}",banMode,senderEntry.name));
            } else {
                // check against names in ban list (may not be current player names)
                string pattern = arg.ToUpper();
                var banList=MPBanPlayers.GetList(banMode);
                int cnt = banList.RemoveAll(entry => (MatchPlayerName(entry.name, pattern) != 0));
                if (cnt > 0) {
                    MPBanPlayers.OnUpdate(banMode, false);
                } else {
                    // check the currently connected players
                    if (SelectPlayer(arg)) {
                        cnt = MPBanPlayers.Unban(selectedPlayerEntry, banMode);
                    }
                }

                if (cnt > 0) {
                    ReturnTo(String.Format("{0} players UNBANNED from {1} list by {2}", cnt, banMode, senderEntry.name));
                } else {
                    ReturnToSender(String.Format("Un{0}: no player {1} found",banMode, arg));
                }
            }
            return false;
        }

        // Execute END command
        public bool DoEnd()
        {
            Debug.Log("END request via chat command");
            ReturnTo(String.Format("manual match END request by {0}",senderEntry.name));
            NetworkMatch.End();
            return false;
        }

        // Execute Extend commad
        public bool DoStartExtend(bool start)
        {
            String op = (start)?"START":"EXTEND";

            if (!inLobby) {
                Debug.LogFormat("{0} request via chat command ignored in non-LOBBY state",op);
                ReturnToSender(String.Format("{0} rejected: not in Lobby",op));
                return false;
            }
            int seconds;
            if (!int.TryParse(arg, NumberStyles.Number, CultureInfo.InvariantCulture, out seconds)) {
                seconds = (start)?5:180;
            }
            Debug.LogFormat("{0} request via chat command: {1} seconds",op, seconds);
            FieldInfo haiInfo = AccessTools.Field(typeof(NetworkMatch),"m_host_active_info");
            var hai = haiInfo.GetValue(null);
            if (hai != null) {
                FieldInfo startTimeInfo = AccessTools.Field(hai.GetType(), "m_time_absolute_match_start");
                FieldInfo matchTimeOutInfo = AccessTools.Field(hai.GetType(), "m_absolute_match_timeout");
                FieldInfo matchStartInfo = AccessTools.Field(hai.GetType(), "m_time_reached_first_can_start");

                DateTime now = DateTime.Now;
                DateTime startTime = (DateTime)startTimeInfo.GetValue(hai);
                if (start) {
                    startTime = now.AddSeconds((double)seconds);
                } else {
                    startTime = startTime.AddSeconds((double)seconds);
                }
                if (startTime < now) {
                    startTime = now;
                }
                TimeSpan offset = startTime - now;

                startTimeInfo.SetValue(hai, startTime);
                matchTimeOutInfo.SetValue(hai, offset);
                matchStartInfo.SetValue(hai, now);

                ReturnTo(String.Format("manual {0} request by {1}: {2} seconds",op,senderEntry.name,seconds));
                ServerLobbyStatus.SendToTracker(); // update the tracker
            } else {
                Debug.LogFormat("{0} request via chat command ignored: no HostActiveMatchInfo",op);
                ReturnToSender(String.Format("{0} rejected: no HostActiveMatchInfo",op));
            }
            return false;
        }

        // Execute STATUS command
        public bool DoStatus()
        {
            string creator;
            if (MPBanPlayers.MatchCreator != null && !String.IsNullOrEmpty(MPBanPlayers.MatchCreator.name)) {
                creator = MPBanPlayers.MatchCreator.name;
            } else {
                creator = "<UNKNOWN>";
            }

            GetTrustedPlayerIds();
            ReturnToSender(String.Format("STATUS: {0}'s game, your auth: {1}", creator,CheckPermission(senderEntry, sender_conn)));
            ReturnToSender(String.Format("STATUS: bans: {0}, annoys: {1}, blocks: {2}, auth: {3} trust: {4}",
                                         MPBanPlayers.GetList(MPBanMode.Ban).Count,
                                         MPBanPlayers.GetList(MPBanMode.Annoy).Count,
                                         MPBanPlayers.GetList(MPBanMode.BlockChat).Count,
                                         authenticatedPlayers.Count,
                                         trustedPlayers.Count));
            return false;
        }

        // Execute SAY command
        public bool DoSay()
        {
            string msg = (String.IsNullOrEmpty(arg))?"":arg;
            MPChatTools.SendTo(inLobby, msg, -1, -1, false, true, sender_name, sender_team, isTeamMessage, sender_conn);
            return false;
        }

        // Execute TEST command
        public bool DoTest()
        {
            Debug.LogFormat("TEST request for {0}", arg);
            if (!SelectPlayer(arg)) {
                ReturnToSender(String.Format("did not find player {0}",arg));
                return false;
            }
            ReturnToSender(String.Format("found player {0}",selectedPlayerEntry.name));
            return false;
        }

        // Execute SWITCHTEAM command
        public bool DoSwitchTeam()
        {
            selectedPlayerEntry = null;
            selectedPlayerConnectionId = -1;

            if (String.IsNullOrEmpty(arg)) {
                // without argument, the player can request a switch for himself...
                if (!SelectSelf()) {
                    ReturnToSender("SWITCHTEAM: did not find you");
                    return false;
                }
            } else {
                if (!SelectPlayer(arg)) {
                    ReturnToSender(String.Format("SWITCHTEAM: did not find player {0}",arg));
                    return false;
                }
            }

            if (selectedPlayerEntry == null || selectedPlayerConnectionId < 0) {
                ReturnToSender("SWITCHTEAM: could not find the target player");
                return false;
            }

            if (senderEntry == null || String.IsNullOrEmpty(senderEntry.name) || String.IsNullOrEmpty(selectedPlayerEntry.name) || senderEntry.name != selectedPlayerEntry.name) {
                // requesting a switch for another player requires permission
                if (!CheckPermission(senderEntry, sender_conn)) {
                    ReturnToSender(String.Format("You do not have the permission for command {0}!", cmd));
                    Debug.LogFormat("CHATCMD {0}: client is not authenticated!", cmd);
                    return false;
                }
            }

            if (inLobby && (selectedPlayerLobbyData != null)) {
                // requesting a team switch in the lobby
                ReturnTo(String.Format("manual TEAM SWITCH request for {0} by {1}",selectedPlayerEntry.name, senderEntry.name));
                NetworkMatch.OnRequestSwitchTeam(selectedPlayerConnectionId, MPTeams.NextTeam(selectedPlayerLobbyData.m_team));
            } else if (!inLobby && (selectedPlayer != null)) {
                // requesting a team switch in-game
                ReturnTo(String.Format("manual TEAM SWITCH request for {0} by {1}",selectedPlayerEntry.name, senderEntry.name));
                MPTeams.ChangeTeamMessage msg =  new MPTeams.ChangeTeamMessage { netId = selectedPlayer.netId, newTeam = MPTeams.NextTeam(selectedPlayer.m_mp_team) };
                MPTeams_Server_RegisterHandlers.DoChangeTeam(msg);
            } else {
                ReturnToSender("SWITCHTEAM: did not find the player");
                return false;
            }

            return false;
        }

        // Execute LISTPLAYERS command
        public bool DoListPlayers()
        {
            int argIdLow = -1;
            int argIdHigh = -1;
            int cnt = 0;

            if (!String.IsNullOrEmpty(arg)) {
                string[] parts = arg.Split(' ');
                if (parts.Length > 0) {
                    if (!int.TryParse(parts[0], NumberStyles.Number, CultureInfo.InvariantCulture, out argIdLow)) {
                        argIdLow = -1;
                    }
                }
                if (parts.Length > 1) {
                    if (!int.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out argIdHigh)) {
                        argIdHigh = -1;
                    }
                }
                if (argIdHigh < 0) {
                    argIdHigh = argIdLow;
                }
            }

            for (int i = 0; i < NetworkServer.connections.Count; i++) {
                if (argIdLow < 0 || (i >= argIdLow && i <= argIdHigh)) {
                    string name = FindPlayerNameForConnection(i, inLobby);
                    if (!String.IsNullOrEmpty(name)) {
                        ReturnToSender(String.Format("{0}: {1}",i,name));
                        cnt++;
                    }
                }
            }
            if (cnt < 1) {
                ReturnToSender("LISTPLAYERS: no players found");
            }
            return false;
        }
        
        // trim down the fields in candidate so that it doesn't alias authenticated
        // Return values is the same as MPBanEntry.trim
        private int TrimBanCandidate(ref MPBanEntry candidate, MPBanEntry authenticated) {
            int val = candidate.trim(authenticated, "Alias check: ");
            if (val < 0) {
                Debug.LogFormat("BAN rejected because it would affect {0}",authenticated.name);
                ReturnToSender(String.Format("BAN rejected because it would also affect {0}",authenticated.name));
            } else if (val > 0) {
                Debug.LogFormat("BAN entry reduced to {0} to avoid aliasing {1}",candidate.Describe(),authenticated.Describe());
            }
            return val;
        }

        // Trim down the fileds in candidate so that it never aliases an authenticated player
        // Returns true if the candidate entry is still valid (might be trimmed),
        // and false when the trimmed candidate is not valid any more
        private bool TrimBanCandidate(ref MPBanEntry candidate) {
            int val;
            if (MPBanPlayers.MatchCreator != null) {
                val = TrimBanCandidate(ref candidate, MPBanPlayers.MatchCreator);
                if (val < 0) {
                    return false;
                }
            }
            foreach(MPBanEntry e in authenticatedPlayers) {
                val = TrimBanCandidate(ref candidate, e);
                if (val < 0) {
                    return false;
                }
            }
            return true;
        }

        // Send a chat message back to the sender of the command
        // HA: An Elvis reference!
        public bool ReturnToSender(string msg) {
            return MPChatTools.SendTo(inLobby, msg, sender_conn, -1, false, false, "Server", MpTeam.TEAM0, false, sender_conn);
        }

        // Send a chat message to all clients except the given connection id, -1 for all
        public bool ReturnTo(string msg, int connection_id = -1, int except_connection_id = -1, bool authOnly=false) {
            return MPChatTools.SendTo(inLobby, msg, connection_id, except_connection_id, authOnly, false, "Server", MpTeam.TEAM0, false, sender_conn);
        }

        // Select a player by a pattern
        public bool SelectPlayer(string pattern) {
            selectedPlayerEntry = null;
            selectedPlayer = null;
            selectedPlayerLobbyData = null;
            selectedPlayerConnectionId = -1;
            if (inLobby) {
                selectedPlayerLobbyData = FindPlayerInLobby(pattern);
                if (selectedPlayerLobbyData != null) {
                    selectedPlayerEntry = new MPBanEntry(selectedPlayerLobbyData);
                    selectedPlayerConnectionId = selectedPlayerLobbyData.m_id;
                    if (selectedPlayerConnectionId >= NetworkServer.connections.Count) {
                        selectedPlayerConnectionId = -1;
                    }
                    return true;
                }
            } else {
                selectedPlayer = FindPlayer(pattern);
                if (selectedPlayer != null) {
                    selectedPlayerEntry = new MPBanEntry(selectedPlayer);
                    selectedPlayerConnectionId = (selectedPlayer.connectionToClient!=null)?selectedPlayer.connectionToClient.connectionId:-1;
                    if (selectedPlayerConnectionId >= NetworkServer.connections.Count) {
                        selectedPlayerConnectionId = -1;
                    }
                    return true;
                }
            }
            return false;
        }

        // Select the player who sent the request
        public bool SelectSelf() {
            selectedPlayerEntry = null;
            selectedPlayer = null;
            selectedPlayerLobbyData = null;
            selectedPlayerConnectionId = -1;

            if (senderEntry != null) {
                selectedPlayerEntry = senderEntry;
                selectedPlayerConnectionId = sender_conn;
                if (inLobby) {
                    foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                        if (p.Value != null && p.Value.m_id == selectedPlayerConnectionId) {
                            selectedPlayerLobbyData = p.Value;
                            return true;
                        }
                    }
                } else {
                    foreach (var p in Overload.NetworkManager.m_Players) {
                        if (p != null && p.connectionToClient != null && p.connectionToClient.connectionId == selectedPlayerConnectionId) {
                            selectedPlayer = p;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        // Find the player ID string based on a connection ID
        public static string FindPlayerIDForConnection(int conn_id, bool inLobby) {
            MPBanEntry entry = FindPlayerEntryForConnection(conn_id, inLobby);
            if (entry != null && !String.IsNullOrEmpty(entry.id)) {
                return entry.id;
            }
            return "";
        }

        // Find the player name string based on a connection ID
        public static string FindPlayerNameForConnection(int conn_id, bool inLobby) {
            MPBanEntry entry = FindPlayerEntryForConnection(conn_id, inLobby);
            if (entry != null && !String.IsNullOrEmpty(entry.name)) {
                return entry.name;
            }
            return "";
        }

        // Make a MPBanEntry candidate from a connection ID
        public static MPBanEntry FindPlayerEntryForConnection(int conn_id, bool inLobby) {
            if (inLobby) {
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    if (p.Value != null && p.Value.m_id == conn_id) {
                        return new MPBanEntry(p.Value);
                    }
                }
            } else {
                foreach (var p in Overload.NetworkManager.m_Players) {
                    if (p != null && p.connectionToClient != null && p.connectionToClient.connectionId == conn_id) {
                        return new MPBanEntry(p);
                    }
                }
            }
            return null;
        }

        // check if a specific player has chat command permissions
        public static bool CheckPermission(MPBanEntry entry, int connection_id) {
            if (entry == null) {
                return false;
            }
            if (MPBanPlayers.MatchCreator != null && entry.matchesStrict(MPBanPlayers.MatchCreator, "MATCH CREATOR: ")) {
                // the match creator is always authenticated
                // if we have a connection id for the creator, also check it
                // note that this function is called with connection_id = -1 only from
                // MPBanPlayers_AcceptNewConnection.Postfix(), where it is used to
                // check if the new match creator (who is already verified) is the same
                // as the previous one
                if (connection_id >= 0 && MPBanPlayers.MatchCreatorConnectionId >= 0) {
                    if (connection_id == MPBanPlayers.MatchCreatorConnectionId) {
                        Debug.LogFormat("MATCH CREATOR: connection ID {0} is authenticated", connection_id);
                        return true;
                    }
                } else {
                    Debug.LogFormat("MATCH CREATOR: connection id not checked ({0} {1})", connection_id, MPBanPlayers.MatchCreatorConnectionId);
                    return true;
                }
            }

            if (String.IsNullOrEmpty(entry.id)) {
                return false;
            }
            // Trusted Players always have permission
            if (IsTrustedPlayer(entry)) {
                return true;
            }
            return IsAuthenticatedPlayer(entry);
        }

        // Check if a client on a specific connection id is authenticated
        public static bool CheckPermission(int connection_id, bool inLobby) {
            MPBanEntry entry = FindPlayerEntryForConnection(connection_id, inLobby);
            return CheckPermission(entry, connection_id);
        }

        // Match string name version pattern,
        // Allow '?' as wildcard character in pattern, matches every character
        public static int MatchIndexOf(string name, string pattern) {
            int nLen = name.Length;
            int pLen = pattern.Length;
            int cnt;
            int i,j;
            cnt = nLen - pLen + 1;

            if (pLen < 1) {
                // empty pattern matches everything
                return 0;
            }

            for (i=0; i < cnt; i++) {
                bool matches = true;
                for (j=0; j<pLen; j++) {
                    if ( (pattern[j] != '?') && (pattern[j] != name[i+j])) {
                        matches = false;
                        break;
                    }
                }
                if (matches) {
                    return i;
                }
            }
            return -1;
        }

        // Match a player name versus the player name pattern
        // Return 1 on perfect match
        //        0 on no match at all
        // or a negative value with lower value meaning worse match
        public int MatchPlayerName(string name, string pattern)
        {
            if (String.IsNullOrEmpty(name)) {
                // no match possible
                return 0;
            }

            if (name == pattern) {
                // perfect match
                return 1;
            }

            int index = MatchIndexOf(name, pattern);
            if (index >= 0) {
               int extraChars = name.Length - pattern.Length + 1;
               // the earlier the match, the better is the score,
               // the less extra chars, the better the the score
               return -extraChars -(index*100);

            }
            return 0;
        }

        // helper class for parsing player selection data from a user-specified argument string
        private class PlayerSelector {
            public string pattern = null;
            public string namePattern = null;
            public int connectionId = -1;
            public bool valid = false;

            public PlayerSelector(string thePattern) {
                valid = false;
                if (!String.IsNullOrEmpty(thePattern)) {
                    pattern = thePattern.ToUpper().Trim();
                    int idx = pattern.IndexOf("C:");
                    if (idx == 0) {
                        idx += 2;
                    } else {
                        idx = pattern.IndexOf("CONN:");
                        if (idx == 0) {
                            idx += 5;
                        }
                    }
                    if (idx >= 0) {
                        if (idx < pattern.Length) {
                            string num = pattern.Substring(idx);
                            if (!int.TryParse(num, NumberStyles.Number, CultureInfo.InvariantCulture, out connectionId)) {
                                connectionId = -1;
                            }
                            if (connectionId >= 0) {
                                valid = true;
                            }
                        }
                    } else {
                        namePattern = pattern;
                        valid = !String.IsNullOrEmpty(namePattern);
                    }
                }
            }
        }

        // Find the best match for a player
        // Search the active players in game
        // May return null if no match can be found
        public Player FindPlayer(string pattern) {
            if (String.IsNullOrEmpty(pattern)) {
                return null;
            }

            int bestScore = -1000000000;
            Player bestPlayer = null;
            PlayerSelector s = new PlayerSelector(pattern);
            if (s.valid) {
                foreach (var p in Overload.NetworkManager.m_Players) {
                    int score = -1000000000;
                    if (s.connectionId >= 0) {
                        if (p.connectionToClient.connectionId == s.connectionId) {
                            score = 1;
                        }
                    } else if (!String.IsNullOrEmpty(s.namePattern)) {
                        score = MatchPlayerName(p.m_mp_name.ToUpper(), s.namePattern);
                    }
                    if (score > 0) {
                        return p;
                    }
                    if (score < 0 && score > bestScore) {
                        bestScore = score;
                        bestPlayer = p;
                    }
                }
            }
            if (bestPlayer == null) {
                Debug.LogFormat("CHATCMD: did not find a player matching {0}", pattern);
            }
            return bestPlayer;
        }

        // Find the best match for a player
        // Search the active players in the lobby
        // May return null if no match can be found
        public PlayerLobbyData FindPlayerInLobby(string pattern) {
            if (String.IsNullOrEmpty(pattern)) {
                return null;
            }

            int bestScore = -1000000000;
            PlayerLobbyData bestPlayer = null;
            PlayerSelector s = new PlayerSelector(pattern);
            if (s.valid) {
                foreach (KeyValuePair<int, PlayerLobbyData> p in NetworkMatch.m_players) {
                    int score = -1000000000;
                    if (s.connectionId >= 0) {
                        if (p.Value.m_id == s.connectionId) {
                            score = 1;
                        }
                    } else if (!String.IsNullOrEmpty(s.namePattern)) {
                        score = MatchPlayerName(p.Value.m_name.ToUpper(), s.namePattern);
                    }
                    if (score > 0) {
                        return p.Value;
                    }
                    if (score < 0 && score > bestScore) {
                        bestScore = score;
                        bestPlayer = p.Value;
                    }
                }
            }
            if (bestPlayer == null) {
                Debug.LogFormat("CHATCMD: did not find a player matching {0}", pattern);
            }
            return bestPlayer;
        }

        // Check if we have set any MPChatCommand related state
        // Currently, this check only whether the authl list is not empty
        public static bool HasModifiedState()
        {
            return (authenticatedPlayers.Count > 0);
        }

        // Reset the state of the chat commands
        // This clears all authentications
        public static void Reset() {
            Debug.Log("MPChatCommands: clearing command authentications");
            authenticatedPlayers.Clear();
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ProcessLobbyChatMessageOnServer")]
    class MPChatCommands_ProcessLobbyMessage {
        private static bool Prefix(int sender_connection_id, LobbyChatMessage msg) {
            MpTeam team = NetworkMatch.GetTeamFromLobbyData(sender_connection_id);
            MPChatCommand cmd = new MPChatCommand(msg.m_text, sender_connection_id, msg.m_sender_name, team, true);
            return cmd.Execute();
        }
    }

    [HarmonyPatch(typeof(Player), "CmdSendFullChat")]
    class MPChatCommands_ProcessIngameMessage {
        private static bool Prefix(int sender_connection_id, string sender_name, MpTeam sender_team, string msg) {
            MPChatCommand cmd = new MPChatCommand(msg, sender_connection_id, sender_name, sender_team, false);
            return cmd.Execute();
        }
    }
}
