using HarmonyLib;
using Overload;
using System;
using UnityEngine;
using UnityEngine.Networking;

// remove randomness in MP projectile lifetime/speed
namespace GameMod
{
    /*
    class IdHashVal
    {
        public static int ForceId = -1;
        public static int Offset = 0;
        public static float Val(int id, bool neg, float min, float max)
        {
            if (ForceId != -1)
                id = ForceId;
            else
                id += Offset;
            uint hash = xxHashSharp.xxHash.CalculateHash(BitConverter.GetBytes(neg ? -id : id));
            return min + (max - min) * ((hash & 65535) / 65536f);
        }
    }
    */

    [HarmonyPatch(typeof(Projectile), "InitLifetime")]
    class MPProjInitLifetime
    {
        private static bool Prefix(ref float __result, Projectile ___m_proj_info, int ___m_projectile_id)
        {
            if (GameplayManager.IsMultiplayerActive && (GameplayManager.IsDedicatedServer() || MenuManager.m_mp_lan_match))
            {
                __result = ___m_proj_info.m_lifetime_max >= 0f ? (___m_proj_info.m_lifetime_min + ___m_proj_info.m_lifetime_max) / 2 : ___m_proj_info.m_lifetime_min;
                //__result = ___m_proj_info.m_lifetime_max >= 0f ? IdHashVal.Val(___m_projectile_id, false, ___m_proj_info.m_lifetime_min, ___m_proj_info.m_lifetime_max) : ___m_proj_info.m_lifetime_min;
                //Debug.Log($"proj {___m_projectile_id} ofs {IdHashVal.Offset} force {IdHashVal.ForceId} init lifetime {__result}");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Projectile), "InitSpeed")]
    class MPProjInitSpeed
    {
        private static bool Prefix(ref float __result, Projectile ___m_proj_info, int ___m_projectile_id)
        {
            if (GameplayManager.IsMultiplayerActive && (GameplayManager.IsDedicatedServer() || MenuManager.m_mp_lan_match))
            {
                __result = ___m_proj_info.m_init_speed_max >= 0f ? (___m_proj_info.m_init_speed_min + ___m_proj_info.m_init_speed_max) / 2 : ___m_proj_info.m_init_speed_min;
                //__result = ___m_proj_info.m_init_speed_max >= 0f ? IdHashVal.Val(___m_projectile_id, true, ___m_proj_info.m_init_speed_min, ___m_proj_info.m_init_speed_max) : ___m_proj_info.m_init_speed_min;
                //Debug.Log($"proj {___m_projectile_id} ofs {IdHashVal.Offset} force {IdHashVal.ForceId}  init speed {__result}");
                return false;
            }
            return true;
        }
    }

    /*
    [HarmonyPatch(typeof(ProjectileManager), "FireProjectile")]
    class MPProjFireProjectile
    {
        private static void Prefix(int force_id, GameObject owner)
        {
            Player component = owner.GetComponent<Player>();
            Debug.Log("FireProjectile prefix " + force_id + " local " + (component && component.isLocalPlayer) + " client.tick " + Client.m_tick + " server.tick " + component?.m_updated_state.m_tick);
            IdHashVal.ForceId = force_id;
        }
        private static void Postfix()
        {
            IdHashVal.ForceId = -1;
        }
    }

    [HarmonyPatch(typeof(Server), "SendProjectileFiredToClients")]
    class MPProjSend
    {
        private static void Prefix(int id)
        {
            Debug.Log("SendProjectileFiredToClients " + id);
        }
    }

    [HarmonyPatch(typeof(Client), "OnFireProjectileToClient")]
    class MPProjRecv
    {
        private static void Prefix(NetworkMessage msg)
        {
            FireProjectileToClientMessage fireProjectileToClientMessage = msg.ReadMessage<FireProjectileToClientMessage>();
            msg.reader.SeekZero();
            IdHashVal.Offset = fireProjectileToClientMessage.m_id + 1 - Projectile.m_projectile_id_next;
            Debug.Log("OnFireProjectileToClient " + fireProjectileToClientMessage.m_id + " client.tick " + Client.m_tick);
        }
    }
    */
}
