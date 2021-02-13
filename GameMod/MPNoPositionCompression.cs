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
            writer.Write((byte)m_num_snapshots);
            for (int i = 0; i < m_num_snapshots; i++) {
                writer.Write(m_snapshots[i].m_net_id);
                writer.Write(m_snapshots[i].m_pos.x);
                writer.Write(m_snapshots[i].m_pos.y);
                writer.Write(m_snapshots[i].m_pos.z);
                writer.Write(m_snapshots[i].m_rot);
            }
        }

        /// <summary>
        /// Deserialize the snapshot without compression.
        /// </summary>
        /// <param name="reader"></param>
        public override void Deserialize(NetworkReader reader) {
            m_num_snapshots = (int)reader.ReadByte();
            for (int i = 0; i < m_num_snapshots; i++) {
                NetworkInstanceId net_id = reader.ReadNetworkId();
                Vector3 pos = default(Vector3);
                pos.x = reader.ReadSingle();
                pos.y = reader.ReadSingle();
                pos.z = reader.ReadSingle();
                Quaternion rot = reader.ReadQuaternion();
                m_snapshots[i] = new PlayerSnapshot(net_id, pos, rot);
            }
        }

        public int m_num_snapshots;
        public PlayerSnapshot[] m_snapshots = new PlayerSnapshot[16];

        /// <summary>
        /// Create a new player snapshot message from an old player snapshot message.
        /// </summary>
        /// <param name="v"></param>
        public static explicit operator NewPlayerSnapshotToClientMessage(PlayerSnapshotToClientMessage v) {
            return new NewPlayerSnapshotToClientMessage {
                m_num_snapshots = v.m_num_snapshots,
                m_snapshots = v.m_snapshots
            };
        }

        /// <summary>
        /// Create an old player snapshot message from a new player snapshot message.
        /// </summary>
        /// <returns></returns>
        public PlayerSnapshotToClientMessage ToPlayerSnapshotToClientMessage() {
            return new PlayerSnapshotToClientMessage {
                m_num_snapshots = m_num_snapshots,
                m_snapshots = m_snapshots
            };
        }
    }

    /// <summary>
    /// Handle the new player snapshot message.
    /// </summary>
    [HarmonyPatch(typeof(Client), "RegisterHandlers")]
    public class MPNoPositionCompression_RegisterHandlers {
        public static void OnNewPlayerSnapshotToClient(NetworkMessage msg) {
            if (NetworkMatch.GetMatchState() == MatchState.PREGAME || NetworkMatch.InGameplay()) {
                PlayerSnapshotToClientMessage item = msg.ReadMessage<NewPlayerSnapshotToClientMessage>().ToPlayerSnapshotToClientMessage();
                Client.m_PendingPlayerSnapshotMessages.Enqueue(item);
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
    public class MPNoPositionCompression_SendByChannel {
        private static void SendSnapshotsToPlayer(Player player) {
            if (!MPNoPositionCompression.enabled || !MPTweaks.ClientHasTweak(player.connectionToClient.connectionId, "nocompress")) {
                player.connectionToClient.SendByChannel(64, player.m_snapshot_buffer, 1);
                return;
            }

            player.connectionToClient.SendByChannel(MessageTypes.MsgNewPlayerSnapshotToClient, (NewPlayerSnapshotToClientMessage)player.m_snapshot_buffer, 1);
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            bool yielding = true;
            foreach (var code in codes) {
                if (yielding) {
                    if (code.opcode == OpCodes.Ble) {
                        yield return code;

                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPNoPositionCompression_SendByChannel), "SendSnapshotsToPlayer"));

                        yielding = false;
                    } else {
                        yield return code;
                    }
                } else {
                    if (code.opcode == OpCodes.Ret) {
                        yield return code;
                    } // else do nothing.
                }
            }
        }
    }
}
