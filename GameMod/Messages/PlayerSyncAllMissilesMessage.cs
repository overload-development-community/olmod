using GameMod.Metadata;
using GameMod.Objects;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// This message allows for the client to tell the server what its missile inventory was at the time of death.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    public class PlayerSyncAllMissilesMessage : MessageBase {
        public NetworkInstanceId m_player_id;
        public int[] m_missile_ammo;

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1);
            writer.Write(m_player_id);
            writer.Write(m_missile_ammo[0]);
            writer.Write(m_missile_ammo[1]);
            writer.Write(m_missile_ammo[2]);
            writer.Write(m_missile_ammo[3]);
            writer.Write(m_missile_ammo[4]);
            writer.Write(m_missile_ammo[5]);
            writer.Write(m_missile_ammo[6]);
            writer.Write(m_missile_ammo[7]);
        }

        public override void Deserialize(NetworkReader reader) {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_missile_ammo = new int[]
            {
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32()
            };
        }

        /// <summary>
        /// Handles the PlayerSyncAllMissilesMessage on the client.  This allows for clients to know another player's missile counts when they were killed.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ClientHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncAllMissilesMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer) {
                return;
            }

            for (int i = 0; i < 8; i++) {
                player.m_missile_ammo[i] = msg.m_missile_ammo[i];
            }

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Handles the PlayerSyncAllMissilesMessage on the server.  This allows for the server to know another player's missile counts when they were killed, and distribute that information to other clients.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ServerHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncAllMissilesMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || !NetworkServer.active) {
                return;
            }

            for (int i = 0; i < 8; i++) {
                player.m_missile_ammo[i] = msg.m_missile_ammo[i];
            }

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players) {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerSyncAllMissiles, msg);
                }
            }
        }
    }
}
