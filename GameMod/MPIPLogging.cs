using HarmonyLib;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {

    [HarmonyPatch(typeof(Server), "OnAddPlayerMessage")]
    class MPIPLogging_NetworkManager_AddPlayer {
        static void Postfix(NetworkMessage msg) {
            if (Overload.NetworkManager.IsServer()) {
                if (NetworkMatch.m_players.ContainsKey(msg.conn.connectionId)) {
                    var player = NetworkMatch.m_players[msg.conn.connectionId];
                    Debug.Log($"Player connected: {player.m_name} from {msg.conn.address}");
                }
            }
        }
    }
}
