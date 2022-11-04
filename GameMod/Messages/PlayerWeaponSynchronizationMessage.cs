using System;
using GameMod.Metadata;
using GameMod.Objects;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// This message allows for synchronization of what primary or secondary the player has selected.  Required for clients and server to agree on what weapon the player is using for things like sounds, ship model with weapons, etc.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    public class PlayerWeaponSynchronizationMessage : MessageBase {
        public NetworkInstanceId m_player_id;
        public ValueType m_type;
        public int m_value;

        public enum ValueType {
            WEAPON,
            WEAPON_PREV,
            MISSILE,
            MISSILE_PREV
        }

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1);
            writer.Write(m_player_id);
            writer.Write((byte)m_type);
            writer.Write(m_value);
        }

        public override void Deserialize(NetworkReader reader) {
            try {
                var version = reader.ReadByte();
                m_player_id = reader.ReadNetworkId();
                m_type = (ValueType)reader.ReadByte();
                m_value = reader.ReadInt32();
            } catch (Exception) { }
        }

        /// <summary>
        /// Handles the PlayerWeaponSynchronizationMessage on the client.  This allows for other clients to know when a client has changed their primary or secondary weapon.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ClientHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerWeaponSynchronizationMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer || player.c_player_ship.m_dead || player.c_player_ship.m_dying) {
                return;
            }

            SniperPackets.SetWeapon(player, msg);

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Handles the PlayerWeaponSynchronizationMessage on the server.  This allows for the server to know what primary/secondary the client is using, and then forward that information to other clients.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ServerHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerWeaponSynchronizationMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.c_player_ship.m_dead || player.c_player_ship.m_dying) {
                return;
            }

            SniperPackets.SetWeapon(player, msg);

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players) {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerWeaponSynchronization, msg);
                }
            }
        }
    }
}
