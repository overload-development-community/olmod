using System;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    [HarmonyPatch(typeof(PlayerSnapshotToClientMessage), "Serialize")]
    public class MPNoPositionCompression_Serialize {
        public static bool Prefix(PlayerSnapshotToClientMessage __instance, NetworkWriter writer) {
            writer.Write((byte)__instance.m_num_snapshots);
            for (int i = 0; i < __instance.m_num_snapshots; i++) {
                writer.Write(__instance.m_snapshots[i].m_net_id);
                writer.Write(__instance.m_snapshots[i].m_pos.x);
                writer.Write(__instance.m_snapshots[i].m_pos.y);
                writer.Write(__instance.m_snapshots[i].m_pos.z);
                writer.Write(__instance.m_snapshots[i].m_rot);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerSnapshotToClientMessage), "Deserialize")]
    public class MPNoPositionCompression_Deserialize {
        public static bool Prefix(PlayerSnapshotToClientMessage __instance, NetworkReader reader) {
            __instance.m_num_snapshots = (int)reader.ReadByte();
            for (int i = 0; i < __instance.m_num_snapshots; i++) {
                NetworkInstanceId net_id = reader.ReadNetworkId();
                Vector3 pos = default(Vector3);
                pos.x = reader.ReadSingle();
                pos.y = reader.ReadSingle();
                pos.z = reader.ReadSingle();
                Quaternion rot = reader.ReadQuaternion();
                __instance.m_snapshots[i] = new PlayerSnapshot(net_id, pos, rot);
            }
            return false;
        }
    }
}
