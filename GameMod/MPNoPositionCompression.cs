using System.Collections.Generic;
using System.Reflection.Emit;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    /// <summary>
    /// This mod disables position compression when the server supports it.
    /// </summary>
    public class MPNoPositionCompression {
        /// <summary>
        /// Determines whether no position compression is enabled for the current game.
        /// </summary>
        static public bool enabled = true;
        static public NewPlayerSnapshotToClientMessage m_new_snapshot_buffer = new NewPlayerSnapshotToClientMessage();
    }

    public class NewPlayerSnapshot
    {
        public NewPlayerSnapshot()
        {
        }

        public NewPlayerSnapshot(NetworkInstanceId net_id, 
                              Vector3 pos, Quaternion rot,
                              Vector3 vel, Vector3 vrot)
        {
            this.m_net_id = net_id;
            this.m_pos = pos;
            this.m_rot = rot;
            this.m_vel = vel;
            this.m_vrot = vrot;
        }

        public NetworkInstanceId m_net_id;

        public Vector3 m_pos;

        public Quaternion m_rot;

        public Vector3 m_vel;

        public Vector3 m_vrot;
    }

    /// <summary>
    /// A class that handles the new way to serialize and deserialize player snapshots without compression.
    /// </summary>
    public class NewPlayerSnapshotToClientMessage : MessageBase {
        /// <summary>
        /// Serialize the snapshot without compression.
        /// </summary>
        /// <param name="writer"></param>
        public override void Serialize(NetworkWriter writer) {
            writer.Write(NetworkMatch.m_match_elapsed_seconds);
            writer.Write((byte)m_num_snapshots);
            for (int i = 0; i < m_num_snapshots; i++) {
                writer.Write(m_snapshots[i].m_net_id);
                writer.Write(m_snapshots[i].m_pos.x);
                writer.Write(m_snapshots[i].m_pos.y);
                writer.Write(m_snapshots[i].m_pos.z);
                writer.Write(m_snapshots[i].m_rot);
                writer.Write(m_snapshots[i].m_vel.x);
                writer.Write(m_snapshots[i].m_vel.y);
                writer.Write(m_snapshots[i].m_vel.z);
                writer.Write(m_snapshots[i].m_vrot.x);
                writer.Write(m_snapshots[i].m_vrot.y);
                writer.Write(m_snapshots[i].m_vrot.z);
            }
        }

        /// <summary>
        /// Deserialize the snapshot without compression.
        /// </summary>
        /// <param name="reader"></param>
        public override void Deserialize(NetworkReader reader) {
            float timestamp = reader.ReadSingle();
            m_num_snapshots = (int)reader.ReadByte();
            for (int i = 0; i < m_num_snapshots; i++) {
                NetworkInstanceId net_id = reader.ReadNetworkId();
                Vector3 pos = default(Vector3);
                pos.x = reader.ReadSingle();
                pos.y = reader.ReadSingle();
                pos.z = reader.ReadSingle();
                Quaternion rot = reader.ReadQuaternion();
                Vector3 vel = default(Vector3);
                vel.x = reader.ReadSingle();
                vel.y = reader.ReadSingle();
                vel.z = reader.ReadSingle();
                Vector3 vrot = default(Vector3);
                vrot.x = reader.ReadSingle();
                vrot.y = reader.ReadSingle();
                vrot.z = reader.ReadSingle();
                m_snapshots[i] = new NewPlayerSnapshot(net_id, pos, rot,
                                                       vel, vrot);
            }
        }

        public int m_num_snapshots;
        public NewPlayerSnapshot[] m_snapshots = new NewPlayerSnapshot[16];

        /// <summary>
        /// Create a new player snapshot message from an old player snapshot message.
        /// </summary>
        /// <param name="v"></param>
        /*public static explicit operator NewPlayerSnapshotToClientMessage(PlayerSnapshotToClientMessage v) {
            return new NewPlayerSnapshotToClientMessage {
                m_num_snapshots = v.m_num_snapshots,
                //m_snapshots = v.m_snapshots
            };
        }

        /// <summary>
        /// Create an old player snapshot message from a new player snapshot message.
        /// </summary>
        /// <returns></returns>
        public PlayerSnapshotToClientMessage ToPlayerSnapshotToClientMessage() {
            return new PlayerSnapshotToClientMessage {
                m_num_snapshots = m_num_snapshots,
                //m_snapshots = m_snapshots
            };
        }*/
    }

    /// <summary>
    /// Handle the new player snapshot message.
    /// </summary>
    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    public class MPNoPositionCompression_RegisterHandlers {

        // when using the new protocol, this object replaces
        // Client.m_PendingPlayerSnapshotMessages
        //public static Queue<NewPlayerSnapshotToClientMessage> m_PendingPlayerSnapshotMessages = new Queue<NewPlayerSnapshotToClientMessage>();

        public static void OnNewPlayerSnapshotToClient(NetworkMessage msg) {
            if (NetworkMatch.GetMatchState() == MatchState.PREGAME || NetworkMatch.InGameplay()) {
                NewPlayerSnapshotToClientMessage item = msg.ReadMessage<NewPlayerSnapshotToClientMessage>();
                MPClientShipReckoning.m_last_update = item;
                MPClientShipReckoning.m_last_update_time = NetworkMatch.m_match_elapsed_seconds;
            }
        }

        private static void Postfix() {
            if (Client.GetClient() == null) {
                return;
            }

            Client.GetClient().RegisterHandler(MessageTypes.MsgNewPlayerSnapshotToClient, OnNewPlayerSnapshotToClient);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    [HarmonyPatch(typeof(Server), "SendSnapshotsToPlayer")]
    public class MPNoPositionCompression_SendSnapshotsToPlayer{

        public static NewPlayerSnapshot[] m_snapshots = new NewPlayerSnapshot[16];
        public static NewPlayerSnapshotToClientMessage m_snapshot_buffer = new NewPlayerSnapshotToClientMessage();
        public static bool Prefix(Player send_to_player){
            m_snapshot_buffer.m_num_snapshots = 0;
            foreach (Player player in Overload.NetworkManager.m_Players)
            {
                if (!(player == null) && !player.m_spectator && !(player == send_to_player))
                {
                    NewPlayerSnapshot playerSnapshot = m_snapshot_buffer.m_snapshots[m_snapshot_buffer.m_num_snapshots++];
                    playerSnapshot.m_net_id = player.netId;
                    playerSnapshot.m_pos = player.transform.position;
                    playerSnapshot.m_rot = player.transform.rotation;
                    playerSnapshot.m_vel = player.c_player_ship.c_rigidbody.velocity;
                    playerSnapshot.m_vrot = player.c_player_ship.c_rigidbody.angularVelocity;
                }
            }
            if (m_snapshot_buffer.m_num_snapshots > 0)
            {
                send_to_player.connectionToClient.SendByChannel(64, m_snapshot_buffer, 1);
            }
            return false;
        }

    }
}
