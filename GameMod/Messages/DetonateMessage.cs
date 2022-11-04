using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using GameMod.Metadata;
using GameMod.Objects;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// This message allows for the client to tell the server it wants to explode all detonators.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    public class DetonateMessage : MessageBase {
        public NetworkInstanceId m_player_id;

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1);
            writer.Write(m_player_id);
        }

        public override void Deserialize(NetworkReader reader) {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
        }

        /// <summary>
        /// Explode Devastators on the client.
        /// </summary>
        /// <param name="msg"></param>
        public static void ClientHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<DetonateMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null) {
                return;
            }

            CreeperSyncExplode.m_allow_explosions = true;
            ProjectileManager.ExplodePlayerDetonators(player);
            CreeperSyncExplode.m_allow_explosions = false;

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Explode Devastators on the server.
        /// </summary>
        /// <param name="msg"></param>
        public static void ServerHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<DetonateMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer || player.c_player_ship.m_dead || player.c_player_ship.m_dying) {
                return;
            }

            CreeperSyncExplode.m_allow_explosions = true;
            SniperPackets.serverCanDetonate = true;
            ProjectileManager.ExplodePlayerDetonators(player);
            CreeperSyncExplode.m_allow_explosions = false;
            SniperPackets.serverCanDetonate = false;

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players) {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgDetonate, msg);
                }
            }
        }
    }
}
