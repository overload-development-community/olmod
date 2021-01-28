using System;
using Harmony;
using Overload;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod {
    [HarmonyPatch(typeof(Player), "LerpRemotePlayer")]
    class MPClientExtrapolation_LerpRemotePlayer {
        static bool Prefix(Player __instance, PlayerSnapshot A, PlayerSnapshot B, float t) {
            if (__instance.m_lerp_wait_for_respawn_pos) {
                return true;
            }
            Vector3 C_pos = B.m_pos + (B.m_pos - A.m_pos);
            __instance.c_player_ship.c_transform.localPosition = Vector3.Lerp(B.m_pos, C_pos, t);
            __instance.c_player_ship.c_transform.rotation = Quaternion.Slerp(A.m_rot, B.m_rot, t);
            __instance.c_player_ship.c_mesh_collider_trans.localPosition = __instance.c_player_ship.c_transform.localPosition;
            return false;
        }
    }
}
