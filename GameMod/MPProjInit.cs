using Harmony;
using Overload;
using UnityEngine;

namespace GameMod
{
    [HarmonyPatch(typeof(Projectile), "InitLifetime")]
    class MPProjInitLifetime
    {
        private static bool Prefix(ref float __result, Projectile ___m_proj_info)
        {
            if (GameplayManager.IsMultiplayerActive && (GameplayManager.IsDedicatedServer() || MenuManager.m_mp_lan_match))
            {
                __result = ___m_proj_info.m_lifetime_max >= 0f ? (___m_proj_info.m_lifetime_min + ___m_proj_info.m_lifetime_max) / 2 : ___m_proj_info.m_lifetime_min;
                //Debug.Log($"proj init lifetime {__result}");
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Projectile), "InitSpeed")]
    class MPProjInitSpeed
    {
        private static bool Prefix(ref float __result, Projectile ___m_proj_info)
        {
            if (GameplayManager.IsMultiplayerActive && (GameplayManager.IsDedicatedServer() || MenuManager.m_mp_lan_match))
            {
                __result = ___m_proj_info.m_init_speed_max >= 0f ? (___m_proj_info.m_init_speed_min + ___m_proj_info.m_init_speed_max) / 2 : ___m_proj_info.m_init_speed_min;
                //Debug.Log($"proj init speed {__result}");
                return false;
            }
            return true;
        }
    }
}
