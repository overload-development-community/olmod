using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod
{
    public static class MPWeaponCycling
    {
        // whether a weapon is included or excluded from cycling
        public static bool[] CPrimaries = new bool[8];
        public static bool[] CSecondaries = new bool[8];

        // translates from position of checkbox to actual index
        public static int[] pPos = new int[8];
        public static int[] mPos = new int[8];

        public static bool PBypass;

        // unscrambles the menu order back into the game's expected order
        public static void UpdateWeaponOrder()
        {
            for (int a = 0; a < 8; a++)
            {
                for (int b = 0; b < 8; b++)
                {
                    if (((WeaponType)a).ToString().Equals(MPAutoSelection.PrimaryPriorityArray[b]))
                    {
                        pPos[b] = a;
                    }
                    if (((MissileType)a).ToString().Equals(MPAutoSelection.SecondaryPriorityArray[b]))
                    {
                        mPos[b] = a;
                    }
                }
            }
        }

        public static void SetPrimaryCycleEnable(int i)
        {
            CPrimaries[pPos[i]] = !CPrimaries[pPos[i]];
            if (CPrimaries[pPos[i]])
            {
                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
            }
            else
            {
                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
            }
            ExtendedConfig.Section_WeaponCycling.Set(true);
        }

        public static void SetSecondaryCycleEnable(int i)
        {
            CSecondaries[mPos[i]] = !CSecondaries[mPos[i]];
            if (CSecondaries[mPos[i]])
            {
                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_picker, 0.8f, 0f, 0f, false);
            }
            else
            {
                SFXCueManager.PlayCue2D(SFXCue.hud_weapon_cycle_close, 0.8f, 0f, 0f, false);
            }
            ExtendedConfig.Section_WeaponCycling.Set(true);
        }
    }

    // full replacement for Player.NextWeapon()
    [HarmonyPatch(typeof(Player), "NextWeapon")]
    class MPWeaponCycling_Player_NextWeapon
    {
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(Player __instance, bool prev = false)
        {
            if (NumAvailablePrimaries(__instance) > 1 && (!__instance.OnlyAmmoWeapons() || (int)__instance.m_ammo != 0))
            {
                WeaponType curr = __instance.m_weapon_type;
                int next = (int)curr;
                for (int i = 0; i < 9; i++) // try all 8 slots then give up and go back to the first
                {
                    next = (next + ((!prev) ? 1 : 7)) % 8;
                    if ((MPWeaponCycling.CPrimaries[next] || MPWeaponCycling.PBypass) && __instance.m_weapon_level[next] != WeaponUnlock.LOCKED)
                    {
                        __instance.Networkm_weapon_type = (WeaponType)next;
                        if (__instance.CanFireWeapon())
                        {
                            __instance.c_player_ship.WeaponSelectFX();
                            __instance.UpdateCurrentWeaponName();
                            break;
                        }
                        else
                        {
                            __instance.Networkm_weapon_type = curr; // because CanFireWeapon() is dumber than CanFireMissileAmmo()
                        }
                    }
                }
            }
            return false;
        }

        // replacement for Player.NumUnlockedWeapons()
        public static int NumAvailablePrimaries(Player p)
        {
            int num = 0;
            for (int i = 0; i < 8; i++)
            {
                if (p.m_weapon_level[i] != 0 && MPWeaponCycling.CPrimaries[i])
                {
                    num++;
                }
            }
            return num;
        }
    }

    // full replacement for Player.SwitchToNextMissileWithAmmo()
    [HarmonyPatch(typeof(Player), "SwitchToNextMissileWithAmmo")]
    class MPWeaponCycling_Player_SwitchToNextMissileWithAmmo
    {
        [HarmonyPriority(Priority.Last)]
        public static bool Prefix(Player __instance, bool prev = false)
        {
            if (NumAvailableSecondaries(__instance) > 1 || (NumAvailableSecondaries(__instance) == 1 && (int)__instance.m_missile_ammo[(int)__instance.m_missile_type] == 0))
            {
                int next = (int)__instance.m_missile_type;
                for (int i = 0; i < 9; i++) // try all 8 slots then give up and go back to the first
                {
                    next = (next + ((!prev) ? 1 : 7)) % 8;
                    if (MPWeaponCycling.CSecondaries[next] && (__instance.m_missile_level[next] != WeaponUnlock.LOCKED) && (__instance.CanFireMissileAmmo((MissileType)next)) && (next != (int)__instance.m_old_missile_type))
                    {
                        __instance.Networkm_missile_type = (MissileType)next;
                        __instance.c_player_ship.MissileSelectFX();
                        __instance.UpdateCurrentMissileName();
                        break;
                    }
                }
            }
            return false;
        }

        // replacement for Player.NumUnlockedMissilesWithAmmo()
        public static int NumAvailableSecondaries(Player p)
        {
            int num = 0;
            for (int i = 0; i < 8; i++)
            {
                if (p.m_missile_level[i] != 0 && (int)p.m_missile_ammo[i] > 0 && MPWeaponCycling.CSecondaries[i])
                {
                    num++;
                }
            }
            return num;
        }
    }

    // allows the exclusion list to be bypassed if player is out of ammo
    [HarmonyPatch(typeof(Player), "SwitchToEnergyWeapon")]
    class MPWeaponCycling_Player_SwitchToEnergyWeapon
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            MPWeaponCycling.PBypass = true;
        }

        public static void Postfix()
        {
            MPWeaponCycling.PBypass = false;
        }
    }

    // allows the exclusion list to be bypassed if player is out of energy
    [HarmonyPatch(typeof(Player), "SwitchToAmmoWeapon")]
    class MPWeaponCycling_Player_SwitchToAmmoWeapon
    {
        [HarmonyPriority(Priority.First)]
        public static void Prefix()
        {
            MPWeaponCycling.PBypass = true;
        }

        public static void Postfix()
        {
            MPWeaponCycling.PBypass = false;
        }
    }
}