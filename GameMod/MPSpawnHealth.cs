using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{
    // Allow clients to spawn with less than 100 health
    public class MPSpawnHealth
    {
        public const int MIN_HEALTH = 10;
        public const int MAX_HEALTH = 100;

        public static readonly Dictionary<int, int> SpawnHealthByConnectionId = new Dictionary<int, int>();
        public static readonly Dictionary<Player, int> LastSpawnHealthByPlayer = new Dictionary<Player, int>();

        public static int GetHealthForConnectionId(int connectionId) => SpawnHealthByConnectionId.TryGetValue(connectionId, out int health) ? health : MAX_HEALTH;

        public static int GetDisplayedHealth(Player player) => LastSpawnHealthByPlayer.TryGetValue(player, out int health) ? health : MAX_HEALTH;

        public static void SetDisplayedHealth(Player player, int health) => LastSpawnHealthByPlayer[player] = health;

        public static bool AnyPlayerHasReducedHealth => LastSpawnHealthByPlayer.Any(entry => entry.Key && entry.Value < MAX_HEALTH);

        public static int ClampToValidHealth(int health) => Mathf.Clamp(health, MIN_HEALTH, MAX_HEALTH);

        public static void Reset(){
            SpawnHealthByConnectionId.Clear();
            LastSpawnHealthByPlayer.Clear();
        }

        // Server
        public static void SendToSupportingClients(SpawnHealthMessage message){
            foreach (var connection in NetworkServer.connections)
                if (connection != null && MPTweaks.ClientHasTweak(connection.connectionId, "spawnhealth"))
                    connection.Send(MessageTypes.MsgSpawnHealth, message);
        }

        // Client
        public static void SendLocalSpawnHealthPreferenceToServer(){
            var client = Client.GetClient();
            if (client != null && client.isConnected)
                client.Send(MessageTypes.MsgSpawnHealth,
                    new SpawnHealthMessage { lobby_id = NetworkMatch.m_my_lobby_id, health = Menus.mms_spawn_health });
        }

        public static void CycleMenuOption(){
            Menus.mms_spawn_health += UIManager.m_select_dir * 10;
            if (Menus.mms_spawn_health > MAX_HEALTH)
                Menus.mms_spawn_health = MIN_HEALTH;
            else if (Menus.mms_spawn_health < MIN_HEALTH)
                Menus.mms_spawn_health = MAX_HEALTH;
            MenuManager.PlayCycleSound(1f, (float)UIManager.m_select_dir);
            SendLocalSpawnHealthPreferenceToServer();
        }

        // the applied value reaches clients indirectly over RpcSetHitpoints in Player.Update
        public static void ApplyHealthOnServer(Player player)
        {
            if (!Overload.NetworkManager.IsServer() || player == null || player.m_spectator || player.connectionToClient == null)
                return;

            int health = ClampToValidHealth(GetHealthForConnectionId(player.connectionToClient.connectionId));

            SetDisplayedHealth(player, health);
            player.m_hitpoints = (float)health;
            //Debug.LogFormat("Server: MPSpawnHealth: spawned {0} with {1} hp", player.m_mp_name, health);
        }

        public class SpawnHealthMessage : MessageBase{
            public int lobby_id;
            public int health;

            public override void Serialize(NetworkWriter writer){
                writer.WritePackedUInt32((uint)lobby_id);
                writer.WritePackedUInt32((uint)health);
            }

            public override void Deserialize(NetworkReader reader){
                lobby_id = (int)reader.ReadPackedUInt32();
                health = (int)reader.ReadPackedUInt32();
            }
        }
    }

    ////////////////////////////////////////
    //              CLIENT
    ////////////////////////////////////////

    // apply the reduced health after the ship has been restored 
    [HarmonyPatch(typeof(Client), "OnRespawnMsg")]
    class MPSpawnHealth_Client_OnRespawnMsg
    {
        static void Postfix(NetworkMessage msg)
        {
            if (Overload.NetworkManager.IsServer())
                return;
            msg.reader.SeekZero();
            RespawnMessage respawnMessage = msg.ReadMessage<RespawnMessage>();
            GameObject playerObject = ClientScene.FindLocalObject(respawnMessage.m_net_id);
            Player player = playerObject == null ? null : playerObject.GetComponent<Player>();

            if (player == null || player.m_spectator)
                return;

            int health = MPSpawnHealth.GetHealthForConnectionId(respawnMessage.lobby_id);
            MPSpawnHealth.SetDisplayedHealth(player, health);
            if (health < MPSpawnHealth.MAX_HEALTH)
                player.m_hitpoints = (float)health;
        }
    }

    [HarmonyPatch(typeof(NetworkMatch), "InitBeforeEachMatch")]
    class MPSpawnHealth_NetworkMatch_InitBeforeEachMatch
    {
        static void Postfix()
        {
            MPSpawnHealth.Reset();
        }
    }

    [HarmonyPatch(typeof(Client), "SendPlayerLoadoutToServer")]
    class MPSpawnHealth_Client_SendPlayerLoadoutToServer
    {
        static void Postfix()
        {
            MPSpawnHealth.SendLocalSpawnHealthPreferenceToServer();
        } 
    }

    // keep local spawn health dic updated
    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    class MPSpawnHealth_Client_RegisterHandlers
    {
        static void Postfix()
        {
            Client.GetClient()?.RegisterHandler(MessageTypes.MsgSpawnHealth, OnClientReceivedSpawnHealth);
        }
            
        static void OnClientReceivedSpawnHealth(NetworkMessage rawMessage)
        {
            var message = rawMessage.ReadMessage<MPSpawnHealth.SpawnHealthMessage>();
            MPSpawnHealth.SpawnHealthByConnectionId[message.lobby_id] = message.health;
        }
    }


    ////////////////////////////////////////
    //              SERVER
    ////////////////////////////////////////

    // send spawn health values of the players to clients that support this feature
    [HarmonyPatch(typeof(Server), "SendLoadoutDataToClients")]
    class MPSpawnHealth_Server_SendLoadoutDataToClients
    {
        static void Postfix()
        {
            foreach (var entry in MPSpawnHealth.SpawnHealthByConnectionId)
                MPSpawnHealth.SendToSupportingClients(
                    new MPSpawnHealth.SpawnHealthMessage { lobby_id = entry.Key, health = entry.Value });
        }
    }

    // apply changed health after ship data setup
    [HarmonyPatch(typeof(Player), "RestorePlayerShipDataAfterRespawn")]
    class MPSpawnHealth_Player_RestorePlayerShipDataAfterRespawn
    {
        static void Postfix(Player __instance) => MPSpawnHealth.ApplyHealthOnServer(__instance);
    }

    // the initial spawn does not trigger RestorePlayerShipDataAfterRespawn
    [HarmonyPatch(typeof(Server), "RespawnPlayer")]
    class MPSpawnHealth_Server_RespawnPlayer
    {
        static void Postfix(Player player){
            MPSpawnHealth.ApplyHealthOnServer(player);
        } 
    }

    // potentionally remove old connection_id, health tuples
    [HarmonyPatch(typeof(Server), "OnConnect")]
    class MPSpawnHealth_Server_OnConnect
    {
        static void Postfix(NetworkMessage msg)
        {
            int connectionId = msg.conn.connectionId;
            if (MPSpawnHealth.SpawnHealthByConnectionId.Remove(connectionId))
                MPSpawnHealth.SendToSupportingClients(new MPSpawnHealth.SpawnHealthMessage
                {
                    lobby_id = connectionId,
                    health = MPSpawnHealth.MAX_HEALTH
                });
        }
    }

    // decode and apply received spawn health packets from clients
    [HarmonyPatch(typeof(Server), "RegisterHandlers")]
    class MPSpawnHealth_Server_RegisterHandlers
    {
        static void Postfix(){
            NetworkServer.RegisterHandler(MessageTypes.MsgSpawnHealth, OnServerReceivedSpawnHealth);
        }

        static void OnServerReceivedSpawnHealth(NetworkMessage rawMessage)
        {
            var message = rawMessage.ReadMessage<MPSpawnHealth.SpawnHealthMessage>();
            message.health = MPSpawnHealth.ClampToValidHealth(message.health);
            MPSpawnHealth.SpawnHealthByConnectionId[message.lobby_id] = message.health;
            MPSpawnHealth.SendToSupportingClients(message);
        }
    }


}
