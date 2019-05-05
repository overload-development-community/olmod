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
        private static IEnumerator MatchStart(int connectionId)
        {
            int pregameWait = 3;
            Server.SendLoadoutDataToClients();
            IntegerMessage durationMsg = new IntegerMessage(pregameWait * 1000);
            NetworkServer.SendToClient(connectionId, CustomMsgType.StartPregameCountdown, durationMsg);
            Debug.Log("JIP: sending start pregame countdown");
            yield return new WaitForSeconds(pregameWait);
            IntegerMessage modeMsg = new IntegerMessage((int)NetworkMatch.GetMode());
            NetworkServer.SendToClient(connectionId, CustomMsgType.MatchStart, modeMsg);
            NetworkSpawnPlayer.Respawn(Server.FindPlayerByConnectionId(connectionId).c_player_ship);
            foreach (Player player in Overload.NetworkManager.m_Players)
            {
                if (player.connectionToClient.connectionId != connectionId)
                {
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
}
