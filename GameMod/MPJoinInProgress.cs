using Harmony;
using Overload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod
{
    class MPJoinInProgress
    {
        public static bool NetworkMatchEnabled = true;
        public static bool MenuManagerEnabled = true;

        public static bool MatchHasStartedMod(bool m_match_has_started)
        {
            return m_match_has_started && !MPJoinInProgress.NetworkMatchEnabled;
        }

        public static string NetworkMatchLevelName()
        {
            int m_match_force_playlist_level_idx = (int)typeof(NetworkMatch).GetField("m_match_force_playlist_level_idx", AccessTools.all).GetValue(null);
            if (m_match_force_playlist_level_idx < 0 || m_match_force_playlist_level_idx >= GameManager.MultiplayerMission.NumLevels)
                return null;
            if (GameManager.MultiplayerMission.IsLevelAnAddon(m_match_force_playlist_level_idx))
                return GameManager.MultiplayerMission.GetAddOnLevelIdStringHash(m_match_force_playlist_level_idx);
            return GameManager.MultiplayerMission.GetLevelFileName(m_match_force_playlist_level_idx);
        }
    }

    class Trans
    {
        public static IEnumerable<CodeInstruction> FieldReadModifier(string fieldName, MethodInfo method, IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                yield return code;
                if (code.opcode == OpCodes.Ldfld && ((FieldInfo)code.operand).Name == fieldName)
                    yield return new CodeInstruction(OpCodes.Call, method);
            }
        }

        public static IEnumerable<CodeInstruction> ReplaceCall(string origMethodName, MethodInfo newMethodInfo, IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && ((MethodInfo)code.operand).Name == origMethodName)
                    code.operand = newMethodInfo;
                yield return code;
            }
        }
    }

    [HarmonyPatch]
    class JIPStartBackfill
    {
        private static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all).GetMethod("StartMatchmakerBackfill");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch]
    class JIPDoTick
    {
        private static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all).GetMethod("DoTick");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch]
    class JIPOnUpdateGS
    {
        private static MethodBase TargetMethod()
        {
            return typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all).GetMethod("OnUpdateGameSession");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "ProcessLobby")]
    class JIPProcessLobby
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "NetSystemOnUpdateGameSession")]
    class JIPNetOnUpdateGS
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "NetSystemNotifyPlayerConnected")]
    class JIPPlayerConn
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.FieldReadModifier("m_match_has_started", AccessTools.Method(typeof(MPJoinInProgress), "MatchHasStartedMod"), codes);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "AcceptNewConnection")]
    class JIPAcceptConn
    {
        public static bool MaybeInLobby()
        {
            return MPJoinInProgress.NetworkMatchEnabled || NetworkMatch.InLobby();
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            return Trans.ReplaceCall("InLobby", AccessTools.Method(typeof(JIPAcceptConn), "MaybeInLobby"), codes);
        }
    }

    [HarmonyPatch(typeof(Server), "OnPlayerJoinLobbyMessage")]
    class JIPJoinLobby
    {
        private static void Postfix(NetworkMessage msg)
        {
            if (!NetworkMatch.InLobby())
            {
                int connectionId = msg.conn.connectionId;
                StringMessage levelNameMsg = new StringMessage(MPJoinInProgress.NetworkMatchLevelName());
                NetworkServer.SendToClient(connectionId, CustomMsgType.SceneLoad, levelNameMsg);
                StringMessage sceneNameMsg = new StringMessage(GameplayManager.m_level_info.SceneName);
                NetworkServer.SendToClient(connectionId, CustomMsgType.SceneLoaded, sceneNameMsg);
                Debug.Log("JIP: sending scene load " + levelNameMsg.value);
                Debug.Log("JIP: sending scene loaded " + sceneNameMsg.value);
            }
        }
    }

    [HarmonyPatch(typeof(Server), "OnReadyForCountdownToServer")]
    class JIPReadyForCountdown
    {
        private static void SendMatchState(int connectionId)
        {
            var n = Overload.NetworkManager.m_Players.Count;
            var msg = new MatchStateMessage() {
                m_match_elapsed_seconds = NetworkMatch.m_match_elapsed_seconds, 
                m_player_states = new PlayerMatchState[n] };
            int i = 0;
            foreach (var player in Overload.NetworkManager.m_Players)
                msg.m_player_states[i++] = new PlayerMatchState() {
                    m_net_id = player.netId, m_kills = player.m_kills, m_deaths = player.m_deaths, m_assists = player.m_assists };
            NetworkServer.SendToClient(connectionId, ModCustomMsg.MsgSetMatchState, msg);
        }

        private static IEnumerator MatchStart(int connectionId)
        {
            var newPlayer = Server.FindPlayerByConnectionId(connectionId);
            if (newPlayer.m_mp_name.StartsWith("OBSERVER")) {
                Debug.LogFormat("Enabling spectator for {0}", newPlayer.m_mp_name);
                newPlayer.Networkm_spectator = true;
                Debug.LogFormat("Enabled spectator for {0}", newPlayer.m_mp_name);
            }

            int pregameWait = newPlayer.Networkm_spectator ? 0 : 3;
            //Debug.Log("SendLoadoutDataToClients: " + NetworkMatch.m_player_loadout_data.Join());
            // restore lobby_id which got wiped out in Client.OnSetLoadout
            foreach (var idData in NetworkMatch.m_player_loadout_data)
                idData.Value.lobby_id = idData.Key;
            Server.SendLoadoutDataToClients();
            IntegerMessage durationMsg = new IntegerMessage(pregameWait * 1000);
            NetworkServer.SendToClient(connectionId, CustomMsgType.StartPregameCountdown, durationMsg);
            Debug.Log("JIP: sending start pregame countdown");
            yield return new WaitForSeconds(pregameWait);
            IntegerMessage modeMsg = new IntegerMessage((int)NetworkMatch.GetMode());
            NetworkServer.SendToClient(connectionId, CustomMsgType.MatchStart, modeMsg);
            SendMatchState(connectionId);
            NetworkSpawnPlayer.Respawn(newPlayer.c_player_ship);
            foreach (Player player in Overload.NetworkManager.m_Players)
            {
                if (player.connectionToClient.connectionId == connectionId)
                    continue;
                // Resend mode for existing player to move h2h -> anarchy
                NetworkServer.SendToClient(player.connectionToClient.connectionId, CustomMsgType.MatchStart, modeMsg);

                if (!newPlayer.m_spectator)
                    player.CallTargetAddHUDMessage(player.connectionToClient, String.Format(Loc.LS("{0} JOINED MATCH"), newPlayer.m_mp_name), -1, true);

                //Debug.Log("JIP: spawning on new client net " + player.netId + " lobby " + player.connectionToClient.connectionId);
                NetworkServer.SendToClient(connectionId, CustomMsgType.Respawn, new RespawnMessage
                {
                    m_net_id = player.netId,
                    lobby_id = player.connectionToClient.connectionId,
                    m_pos = player.transform.position,
                    m_rotation = player.transform.rotation,
                    use_loadout1 = player.m_use_loadout1
                });
            }
        }

        private static void Postfix(NetworkMessage msg)
        {
            if (NetworkMatch.GetMatchState() == MatchState.PLAYING)
            {
                GameManager.m_gm.StartCoroutine(MatchStart(msg.conn.connectionId));
            }
        }
    }

    /*
    Fix for unable to rejoin with multiple players on same pc
    doesn't work because OnUpdateGameSession uses only (pc)playerId for dup check
    and restores m_has_connected / m_time_slot_reserved if same playerId is already connected :(
    [HarmonyPatch]
    class JIPRejoin
    {
        private static Type CPIType;
        private static Type HPMIType;

        private static MethodBase TargetMethod()
        {
            Type HAMIType = typeof(NetworkMatch).GetNestedType("HostActiveMatchInfo", AccessTools.all);
            CPIType = HAMIType.GetNestedType("ConnectedPlayerInfo", AccessTools.all);
            HPMIType = typeof(NetworkMatch).GetNestedType("HostPlayerMatchmakerInfo", AccessTools.all); 
            return HAMIType.GetMethod("NotifyPlayerDisconnected");
        }

        public static void DisconnectConnectedPlayerInfo(object cip)
        {
            object hpmi = CPIType.GetProperty("MatchmakerInfo").GetValue(cip, null);
            Debug.LogFormat("DisconnectConnectedPlayerInfo, m_has_connected was {0}", HPMIType.GetField("m_has_connected").GetValue(hpmi));
            HPMIType.GetField("m_has_connected").SetValue(hpmi, false);
            HPMIType.GetField("m_time_slot_reserved").SetValue(hpmi, new DateTime(0)); // force timeout
            Debug.LogFormat("DisconnectConnectedPlayerInfo, m_has_connected is {0}", HPMIType.GetField("m_has_connected").GetValue(hpmi));
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0; // 0 = before first label, 1 = after first label
            foreach (var code in codes)
            {
                if (state == 0 && code.labels.Count != 0) // after if
                {
                    var c = new CodeInstruction(OpCodes.Ldloc_0) { labels = code.labels };
                    code.labels = new List<Label>();
                    yield return c;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JIPRejoin), "DisconnectConnectedPlayerInfo"));
                    state = 1;
                }
                yield return code;
            }
        }
    }
    */

    [HarmonyPatch(typeof(UIElement), "DrawMpMatchSetup")]
    class JIPMatchSetup
    {
        public static void DrawMpMatchJIP(UIElement uie, ref Vector2 position)
        {
            uie.SelectAndDrawStringOptionItem(Loc.LS("ALLOW JOIN IN PROGRESS"), position, 7,
                MenuManager.GetToggleSetting(MPJoinInProgress.MenuManagerEnabled ? 1 : 0),
                Loc.LS("ALLOW PLAYERS TO JOIN MATCH AFTER IT HAS STARTED"), 1.5f, !MenuManager.m_mp_lan_match);
            position.y += 62f;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldstr && (string)code.operand == "POWERUP SETTINGS")
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldloca, 0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JIPMatchSetup), "DrawMpMatchJIP"));
                }
                yield return code;
            }
        }
    }

    [HarmonyPatch(typeof(MenuManager), "MpMatchSetup")]
    class JIPMatchSetupHandle
    {
        static void Postfix()
        {
            var prev_dir = UIManager.m_select_dir;
            if (MenuManager.m_menu_sub_state == MenuSubState.ACTIVE &&
                (UIManager.PushedSelect(100) || UIManager.PushedDir()) &&
                MenuManager.m_menu_micro_state == 3 &&
                UIManager.m_menu_selection == 7)
            {
                MPJoinInProgress.MenuManagerEnabled = !MPJoinInProgress.MenuManagerEnabled;
                MenuManager.PlayCycleSound(1f, (float)prev_dir);
            }
        }
    }

    // Send min_num_players 1 instead of 2 for creating join-in-progress match. Depends on FindPrivateMatchGrouping patch in MPMaxPlayer
    [HarmonyPatch(typeof(NetworkMatch), "StartMatchMakerRequest")]
    class JIPMinPlayerPatch
    {
        public static void FinalizeRequest(MatchmakerPlayerRequest req)
        {
            if (MenuManager.m_mp_lan_match && MPJoinInProgress.MenuManagerEnabled && NetworkMatch.m_match_req_password == "")
                req.PlayerAttributes["min_num_players"] = 1;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            LocalBuilder reqVar = null;
            CodeInstruction last = null;
            foreach (var code in codes)
            {
                if (last != null && last.opcode == OpCodes.Newobj && ((ConstructorInfo)last.operand).ReflectedType == typeof(MatchmakerPlayerRequest) &&
                    (code.opcode == OpCodes.Stloc || code.opcode == OpCodes.Stloc_S))
                    reqVar = (LocalBuilder)code.operand;
                if (reqVar != null && code.opcode == OpCodes.Ldsfld && ((FieldInfo)code.operand).Name == "m_system_game_client")
                {
                    yield return new CodeInstruction(OpCodes.Ldloc, reqVar);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(JIPMinPlayerPatch), "FinalizeRequest"));
                }
                yield return code;
                last = code;
            }
        }
    }

    public class PlayerMatchState
    {
        public NetworkInstanceId m_net_id;
        public int m_kills;
        public int m_deaths;
        public int m_assists;
    }

    public class MatchStateMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.Write(m_match_elapsed_seconds);
            writer.WritePackedUInt32((uint)m_player_states.Length);
            foreach (var pl_state in m_player_states)
            {
                writer.Write(pl_state.m_net_id);
                writer.WritePackedUInt32((uint)pl_state.m_kills);
                writer.WritePackedUInt32((uint)pl_state.m_deaths);
                writer.WritePackedUInt32((uint)pl_state.m_assists);
            }
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            m_match_elapsed_seconds = reader.ReadSingle();
            var n = reader.ReadPackedUInt32();
            m_player_states = new PlayerMatchState[n];
            for (int i = 0; i < n; i++)
                m_player_states[i] = new PlayerMatchState() {
                    m_net_id = reader.ReadNetworkId(),
                    m_kills = (int)reader.ReadPackedUInt32(),
                    m_deaths = (int)reader.ReadPackedUInt32(),
                    m_assists = (int)reader.ReadPackedUInt32()
                };
        }
        public float m_match_elapsed_seconds;
        public PlayerMatchState[] m_player_states;
    }

    public class ModCustomMsg
    {
        public const short MsgSetMatchState = 101;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class JIPClientHandlers
    {
        private static void OnSetMatchStateMsg(NetworkMessage msg)
        {
            var msmsg = msg.ReadMessage<MatchStateMessage>();
            NetworkMatch.m_match_elapsed_seconds = msmsg.m_match_elapsed_seconds;
            foreach (var pl_state in msmsg.m_player_states) {
                GameObject gameObject = ClientScene.FindLocalObject(pl_state.m_net_id);
                if (gameObject == null)
                    continue;
                var player = gameObject.GetComponent<Player>();
                player.m_kills = pl_state.m_kills;
                player.m_deaths = pl_state.m_deaths;
                player.m_assists = pl_state.m_assists;
            }
            NetworkMatch.SortAnarchyPlayerList();
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(ModCustomMsg.MsgSetMatchState, OnSetMatchStateMsg);
        }
    }
}
