using Harmony;
using Overload;
using UnityEngine;
using System;

namespace GameMod
{
    // Ensure doors have a built-in automap material, needed for custom doors
    [HarmonyPatch(typeof(DoorAnimating), "Start")]
    class CustomDoorAutomap
    {
        private static void Postfix(DoorAnimating __instance, bool ___m_security_door)
        {
            if (!__instance.m_door_automap)
                return;
            Renderer component = __instance.m_door_automap.GetComponent<Renderer>();
            if (!component || component.material == null)
                return;
            Material newMat =
                __instance.m_is_secret ?
                    GameplayManager.m_automap.m_map_camera.m_map_solid_material :
                ___m_security_door ?
                    GameManager.m_local_player.m_unlock_level < __instance.LockType ? 
                        GameplayManager.m_automap.m_map_camera.m_map_door_security_material :
                        GameplayManager.m_automap.m_map_camera.m_map_door_unlocked_material :
                GameplayManager.m_automap.m_map_camera.m_map_door_default_material;
            string curName = component.material.name;
            int i = curName.IndexOf(' ');
            if (i >= 0)
                curName = curName.Substring(0, i);
            if (component.material != newMat &&
                curName.Equals(newMat.name, StringComparison.OrdinalIgnoreCase))
                component.material = newMat;
        }
    }
}
