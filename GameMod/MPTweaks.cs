using Harmony;
using Overload;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Networking.NetworkSystem;

namespace GameMod
{
    class MPTweaks
    {
        public class ClientInfo
        {
            public Dictionary<string, string> Capabilities = new Dictionary<string, string>();
            public HashSet<string> SupportsTweaks = new HashSet<string>();
        }

        private static readonly Dictionary<string, string> oldSettings = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> settings = new Dictionary<string, string>();
        public static readonly Dictionary<int, ClientInfo> ClientInfos = new Dictionary<int, ClientInfo>();
        public static bool IncompatibleMatchReported;

        public static bool ClientHasMod(int connectionId)
        {
            return ClientInfos.TryGetValue(connectionId, out var clientInfo) &&
                clientInfo.Capabilities.ContainsKey("ModVersion");
        }

        public static void Set(Dictionary<string, string> newSettings)
        {
            settings.Clear();
            foreach (var x in newSettings)
                settings.Add(x.Key, x.Value);
            //Debug.Log("MPTweaks.Set " + (Overload.NetworkManager.IsServer() ? "server" : "conn " + NetworkMatch.m_my_lobby_id) + " new " + newSettings.Join() + " settings " + settings.Join());
            if (NetworkMatch.GetMatchState() == MatchState.PLAYING)
                Apply();
        }

        public static void Reset()
        {
            settings.Clear();
        }

        public static void InitMatch()
        {
            IncompatibleMatchReported = false;
            Reset();
            ClientInfos.Clear();
        }

        public static string ApplySetting(string key, string value)
        {
            string[] keyParts = key.Split('.');
            if (keyParts[0] == "proj" && Enum.IsDefined(typeof(ProjPrefab), keyParts[1]))
            {
                var obj = ProjectileManager.proj_info[(int)Enum.Parse(typeof(ProjPrefab), keyParts[1])];
                var oldValue = obj.GetType().GetField(keyParts[2]).GetValue(obj).ToString();
                RUtility.ReadField(obj, keyParts[2], value);
                return oldValue;
            }
            if (key == "ctf.returntimer" && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float valFloat))
            {
                var oldValue = CTF.ReturnTimeAmount.ToStringInvariantCulture();
                CTF.ReturnTimeAmount = valFloat;
                CTF.ShowReturnTimer = true;
                return oldValue;
            }
            if (key == "item.pickupcheck" && bool.TryParse(value, out bool valBool))
            {
                MPPickupCheck.PickupCheck = valBool;
                return Boolean.TrueString;
            }
            return null;
        }

        public static void Apply()
        {
            if (oldSettings.Any())
                Debug.Log("MPTweaks.Apply " + (Overload.NetworkManager.IsServer() ? "server" : "conn " + NetworkMatch.m_my_lobby_id) + " restoring to " + oldSettings.Join());
            foreach (var x in oldSettings)
                ApplySetting(x.Key, x.Value);
            oldSettings.Clear();
            foreach (var x in settings)
                oldSettings[x.Key] = ApplySetting(x.Key, x.Value);
            Debug.Log("MPTweaks.Apply " + (Overload.NetworkManager.IsServer() ? "server" : "conn " + NetworkMatch.m_my_lobby_id) + " settings " + settings.Join() + " oldsettings " + oldSettings.Join());
        }
 
        public static void Send(int conn_id = -1)
        {
            //Debug.Log("MPTweaks.Send to " + conn_id + " settings " + settings.Join());
            var msg = new TweaksMessage { m_settings = settings };
            if (conn_id == -1)
            {
                foreach (var conn in NetworkServer.connections)
                    if (conn != null && ClientHasMod(conn.connectionId))
                        conn.Send(MPTweaksCustomMsg.MsgMPTweaksSet, msg);
            }
            else if (ClientHasMod(conn_id))
               NetworkServer.SendToClient(conn_id, MPTweaksCustomMsg.MsgMPTweaksSet, msg);
        }

        public static ClientInfo ClientCapabilitiesSet(int connectionId, Dictionary<string, string> capabilities)
        {
            var clientInfo = ClientInfos[connectionId] = new ClientInfo { Capabilities = capabilities };
            if (capabilities.TryGetValue("SupportsTweaks", out string supportsTweaks))
                foreach (var tweak in supportsTweaks.Split(','))
                    clientInfo.SupportsTweaks.Add(tweak);
            return clientInfo;
        }

        public static void ClientCapabilitiesRemove(int connectionId)
        {
            ClientInfos.Remove(connectionId);
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MPTweaksInitBeforeEachMatch
    {
        private static void Postfix()
        {
            MPTweaks.InitMatch();
        }
    }

    // send tweaks after mp scene load, needed to make ReadMultiplayerModeFile work for pickupcheck
    // was: NetworkMatch.NetSystemNotifyMatchStart, after start of lobby countdown
    [HarmonyPatch(typeof(Overload.NetworkManager), "LoadScene")]
    class MPTweaksLoadScene
    {
        private static void Postfix()
        {
            if (!GameplayManager.IsDedicatedServer())
                return;
            Debug.Log("MPTweaksLoadScene");
            RobotManager.ReadMultiplayerModeFile();
            Debug.Log("MPTweaks loaded mode file");
            bool nobodySupportsProj = !NetworkMatch.m_players.Keys.Any(connId =>
                MPTweaks.ClientInfos.TryGetValue(connId, out var clientInfo) &&
                    clientInfo.SupportsTweaks.Contains("proj"));
            var tweaks = new Dictionary<string, string>() { };
            if (nobodySupportsProj) // use stock hunters for all stock client match
                Debug.LogFormat("MPTweaks: not tweaking hunter: unsupported by all clients");
            else
                tweaks.Add("proj.missile_hunter.m_init_speed_min", "17.5");
            if (NetworkMatch.GetMode() == CTF.MatchModeCTF)
                tweaks.Add("ctf.returntimer", CTF.ReturnTimeAmountDefault.ToStringInvariantCulture());
            if (!MPCustomModeFile.PickupCheck)
                tweaks.Add("item.pickupcheck", Boolean.FalseString);
            if (tweaks.Any())
            {
                Debug.LogFormat("MPTweaks: sending tweaks {0}", tweaks.Join());
                MPTweaks.Set(tweaks);
                MPTweaks.Send();
            }
        }
    }

    [HarmonyPatch(typeof(GameplayManager), "StartLevel")]
    class MPTweaksStartLevel
    {
        private static void Postfix()
        {
            //Debug.Log("MPTweaksStartLevel");
            if (!GameplayManager.IsMultiplayerActive)
                MPTweaks.Reset();
            MPTweaks.Apply();
        }
    }

    public class TweaksMessage : MessageBase
    {
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)0); // version
            writer.WritePackedUInt32((uint)m_settings.Count);
            foreach (var x in m_settings)
            {
                writer.Write(x.Key);
                writer.Write(x.Value);
            }
        }
        public override void Deserialize(NetworkReader reader)
        {
            var version = reader.ReadByte();
            int count = (int)reader.ReadPackedUInt32();
            if (m_settings == null)
                m_settings = new Dictionary<string, string>();
            m_settings.Clear();
            for (int i = 0; i < count; i++) {
                string key = reader.ReadString();
                string value = reader.ReadString();
                m_settings[key] = value;
            }
        }
        public Dictionary<string, string> m_settings;
    }

    public class MPTweaksCustomMsg
    {
        public const short MsgClientCapabilities = 119;
        public const short MsgMPTweaksSet = 120;
    }

    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPTweaksClientHandlers
    {
        private static void OnMPTweaksSet(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<TweaksMessage>();
            MPTweaks.Set(msg.m_settings);
        }

        static void Postfix()
        {
            if (Client.GetClient() == null)
                return;
            Client.GetClient().RegisterHandler(MPTweaksCustomMsg.MsgMPTweaksSet, OnMPTweaksSet);
        }
    }

    [HarmonyPatch(typeof(Server), "RegisterHandlers")]
    class MPTweaksServerHandlers
    {
        private static void OnClientCapabilities(NetworkMessage rawMsg)
        {
            var msg = rawMsg.ReadMessage<TweaksMessage>();
            Debug.LogFormat("MPTweaks: received client capabilities {0}: {1}", rawMsg.conn.connectionId, msg.m_settings.Join());
            MPTweaks.ClientCapabilitiesSet(rawMsg.conn.connectionId, msg.m_settings);
        }

        static void Postfix()
        {
            NetworkServer.RegisterHandler(MPTweaksCustomMsg.MsgClientCapabilities, OnClientCapabilities);
        }
    }

    // clear client capabilities on connect in case connection is reused
    [HarmonyPatch(typeof(Server), "OnConnect")]
    class MPTweaksServerOnConnect
    {
        static void Postfix(NetworkMessage msg)
        {
            MPTweaks.ClientCapabilitiesRemove(msg.conn.connectionId);
        }
    }

    // send client capabilities for compatible server
    [HarmonyPatch(typeof(NetworkMatch), "OnAcceptedToLobby")]
    class MPTweaksAcceptedToLobby
    {
        private static void Prefix(AcceptedToLobbyMessage accept_msg)
        {
            if (Client.GetClient() == null)
            {
                Debug.Log("MPTweaks OnAcceptedToLobby: no client?");
                return;
            }
            var server = accept_msg.m_server_location;
            if (!server.StartsWith("OLMOD ") ||
                server == "OLMOD 0.2.6") // other server / server too old
            {
                Debug.LogFormat("MPTweaks: unsupported server {0}", server);
                return;
            }
            Debug.Log("MPTweaks: sending client capabilites");
            var caps = new Dictionary<string, string>();
            caps.Add("ModVersion", Core.GameMod.Version);
            caps.Add("SupportsTweaks", "proj");
            Client.GetClient().Send(MPTweaksCustomMsg.MsgClientCapabilities, new TweaksMessage { m_settings = caps } );
        }
    }

    // no capabilities seen when OnLoadoutDataMessage arrives? -> old client
    [HarmonyPatch(typeof(Server), "OnLoadoutDataMessage")]
    class MPTweaksOnLoadoutDataMessage
    {
        private static IEnumerator DisconnectCoroutine(int connectionId)
        {
            yield return new WaitForSecondsRealtime(5);
            var conn = NetworkServer.connections[connectionId];
            if (conn != null)
                conn.Disconnect();
        }

        private static void Postfix(NetworkMessage msg)
        {
            var connId = msg.conn.connectionId;
            if (connId == 0) // ignore local connection
                return;
            if (!MPTweaks.ClientInfos.TryGetValue(connId, out var clientInfo))
                clientInfo = MPTweaks.ClientCapabilitiesSet(connId, new Dictionary<string, string>());
            /*
            if (!clientInfo.SupportsTweaks.Contains("proj") && !MPTweaks.IncompatibleMatchReported)
            {
                MPTweaks.IncompatibleMatchReported = true;
                string name = NetworkMatch.m_players.TryGetValue(connId, out var playerData) ? playerData.m_name : connId.ToString();
                var text = "DISABLED HUNTER SPEED CHANGE! OLD OLMOD: " + name;
                Debug.LogFormat("MPTweaks: IncompatibleMatchReported {0}", text);
                JIPClientHandlers.SendAddMpStatus(text);
            }
            */
            if (!MPTweaks.ClientHasMod(connId) &&
                (int)NetworkMatch.GetMode() > (int)MatchMode.TEAM_ANARCHY ||
                MPTeams.NetworkMatchTeamCount > 2)
            {
                LobbyChatMessage chatMsg = new LobbyChatMessage(connId, "SERVER", MpTeam.ANARCHY, "You need OLMOD to join this match", false);
                NetworkServer.SendToClient(connId, CustomMsgType.LobbyChatToClient, chatMsg);
                GameManager.m_gm.StartCoroutine(DisconnectCoroutine(connId));
            }
        }
    }
}
