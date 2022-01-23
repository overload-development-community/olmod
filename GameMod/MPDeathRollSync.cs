using HarmonyLib;
using Overload;

namespace GameMod
{
    [HarmonyPatch(typeof(PlayerShip), "FixedUpdatePreDying")]
    class MPDeathRollSync_PlayerShip_FixedUpdatePreDying
    {
        static void Prefix(PlayerShip __instance)
        {
            if (GameplayManager.IsMultiplayerActive && __instance.c_mesh_collider_trans != null && __instance.c_transform != null)
                __instance.c_mesh_collider_trans.localPosition = __instance.c_transform.localPosition;
        }
    }
}
