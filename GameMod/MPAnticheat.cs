using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    /*
    [HarmonyPatch(typeof(PlayerShip), "ProcessFiringControls")]
    internal class ProcessFiringControls
    {
        private static bool CanFire(PlayerShip __instance)
        {
            return (MPShips.FireWhileBoost || !__instance.m_boosting) && __instance.m_wheel_select_state == WheelSelectState.NONE;
        }

        private static void Prefix(PlayerShip __instance)
        {
            if (!GameplayManager.IsMultiplayer)
            {
                return;
            }

            //if (!__instance.c_player.JustPressed(CCInput.FIRE_WEAPON))
            if (!(__instance.c_player.JustPressed(CCInput.FIRE_WEAPON) && CanFire(__instance)))
            {
                return;
            }

            if (__instance.c_player.m_overdrive)
            {
                return;
            }

            //if (__instance.c_player.m_weapon_type != WeaponType.CRUSHER && __instance.c_player.m_weapon_type != WeaponType.LANCER) {
            if (MPWeapons.primaries[(int)__instance.c_player.m_weapon_type].firingMode != FiringMode.SEMI_AUTO)
            {
                return;
            }

            if (__instance.m_refire_time > 0)
            {
                __instance.m_refire_time = Mathf.Max(__instance.m_refire_time, 0.05f); // 1f / 20f
            }
            return;
        }
    }
    */

    [HarmonyPatch(typeof(Controls), "ReadControlData")]
    internal class ReadControlData
    {
        private static void Postfix()
        {
            for (var x = 0; x <= 1; x++)
            {
                for (var y = 14; y <= 15; y++)
                {
                    if (Controls.m_input_kc[x, y] == KeyCode.Joystick8Button10 || Controls.m_input_kc[x, y] == KeyCode.Joystick8Button11)
                    {
                        Controls.m_input_kc[x, y] = KeyCode.None;
                    }
                }
            }
        }
    }
}
