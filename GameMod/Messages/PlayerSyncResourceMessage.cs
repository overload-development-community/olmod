using GameMod.Metadata;
using GameMod.Objects;
using Overload;
using UnityEngine.Networking;

namespace GameMod.Messages {
    /// <summary>
    /// This message allows for the client to tell the server - and for the server to subsequently tell other clients - how much of a resource a player has.
    /// TODO: I'm not 100% sure how necessary it is for other clients to know about each others' resource counts.  It may be worth looking into to see what this is needed for on other clients.
    /// </summary>
    [Mod(Mods.SniperPackets)]
    public class PlayerSyncResourceMessage : MessageBase {
        public NetworkInstanceId m_player_id;
        public ValueType m_type;
        public float m_value;

        public enum ValueType {
            ENERGY,
            AMMO,
            FALCON,
            MISSILE_POD,
            HUNTER,
            CREEPER,
            NOVA,
            DEVASTATOR,
            TIME_BOMB,
            VORTEX
        }

        public override void Serialize(NetworkWriter writer) {
            writer.Write((byte)1);
            writer.Write(m_player_id);
            writer.Write((byte)m_type);
            writer.Write(m_value);
        }

        public override void Deserialize(NetworkReader reader) {
            var version = reader.ReadByte();
            m_player_id = reader.ReadNetworkId();
            m_type = (ValueType)reader.ReadByte();
            m_value = reader.ReadSingle();
        }

        /// <summary>
        /// Handles the PlayerSyncResourceMessage on the client.  This allows for other clients to know what the resource counts for a client are.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ClientHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncResourceMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.isLocalPlayer || player.c_player_ship.m_dead || player.c_player_ship.m_dying) {
                return;
            }

            switch (msg.m_type) {
                case PlayerSyncResourceMessage.ValueType.ENERGY:
                    player.m_energy = msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.AMMO:
                    player.m_ammo = (int)msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.FALCON:
                case PlayerSyncResourceMessage.ValueType.MISSILE_POD:
                case PlayerSyncResourceMessage.ValueType.HUNTER:
                case PlayerSyncResourceMessage.ValueType.CREEPER:
                case PlayerSyncResourceMessage.ValueType.NOVA:
                case PlayerSyncResourceMessage.ValueType.DEVASTATOR:
                case PlayerSyncResourceMessage.ValueType.TIME_BOMB:
                case PlayerSyncResourceMessage.ValueType.VORTEX:
                    var missileType = (MissileType)(msg.m_type - PlayerSyncResourceMessage.ValueType.FALCON);
                    player.m_missile_ammo[(int)missileType] = (int)msg.m_value;
                    break;
            }

            // Workaround for invisible players in JIP.  If they are invisible, make them visible if they sent any kind of sniper packet.
            MPJoinInProgress.SetReady(player, true);
        }

        /// <summary>
        /// Handles the PlayerSyncResourceMessage on the server.  This allows for the server to know the quantity of a resource the client is using, and forwards that information to other clients.
        /// </summary>
        /// <param name="rawMsg"></param>
        public static void ServerHandler(NetworkMessage rawMsg) {
            if (!SniperPackets.enabled) return;

            var msg = rawMsg.ReadMessage<PlayerSyncResourceMessage>();
            var player = Overload.NetworkManager.m_Players.Find(p => p.netId == msg.m_player_id);

            if (player == null || player.c_player_ship.m_dead || player.c_player_ship.m_dying) {
                return;
            }

            switch (msg.m_type) {
                case PlayerSyncResourceMessage.ValueType.ENERGY:
                    player.m_energy = msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.AMMO:
                    player.m_ammo = (int)msg.m_value;
                    break;
                case PlayerSyncResourceMessage.ValueType.FALCON:
                case PlayerSyncResourceMessage.ValueType.MISSILE_POD:
                case PlayerSyncResourceMessage.ValueType.HUNTER:
                case PlayerSyncResourceMessage.ValueType.CREEPER:
                case PlayerSyncResourceMessage.ValueType.NOVA:
                case PlayerSyncResourceMessage.ValueType.DEVASTATOR:
                case PlayerSyncResourceMessage.ValueType.TIME_BOMB:
                case PlayerSyncResourceMessage.ValueType.VORTEX:
                    var missileType = (MissileType)(msg.m_type - PlayerSyncResourceMessage.ValueType.FALCON);
                    player.m_missile_ammo[(int)missileType] = (int)msg.m_value;
                    break;
            }

            foreach (Player remotePlayer in Overload.NetworkManager.m_Players) {
                if (player.connectionToClient.connectionId != remotePlayer.connectionToClient.connectionId && Tweaks.ClientHasMod(remotePlayer.connectionToClient.connectionId)) {
                    NetworkServer.SendToClient(remotePlayer.connectionToClient.connectionId, MessageTypes.MsgPlayerSyncResource, msg);
                }
            }
        }
    }
}
