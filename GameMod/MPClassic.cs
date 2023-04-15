using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{

    static class MPClassic
    {
        public static bool matchEnabled { get; set; } = false;
    }

    [HarmonyPatch(typeof(Player), "CanUnlockWeapon")]
    class MPClassic_Player_CanUnlockWeapon
    {
        static bool Prefix(Player __instance, WeaponType wt, ref bool __result)
        {
            if (!GameplayManager.IsMultiplayer || !MPClassic.matchEnabled)
                return true;

            if (__instance.m_weapon_level[(int)wt] == WeaponUnlock.LOCKED || (wt == WeaponType.IMPULSE && __instance.m_weapon_level[(int)wt] == WeaponUnlock.LEVEL_1))
            {
                __result = true;
            }
            else
            {
                __result = false;
            }

            return false;
        }
    }

    /// <summary>
    /// Replace Player::UnlockWeapon() function
    /// </summary>
    [HarmonyPatch(typeof(Player), "UnlockWeapon")]
    class MPClassic_Player_UnlockWeapon
    {
        static bool Prefix(ref bool __result, Player __instance, WeaponType wt, bool silent = false, bool picked_up = false)
        {
            if (!GameplayManager.IsMultiplayer || !MPClassic.matchEnabled)
                return true;

            if (!NetworkServer.active)
            {
                Debug.LogWarning("[Server] function 'System.Boolean Overload.Player::UnlockWeapon(Overload.WeaponType,System.Boolean,System.Boolean)' called on client");
                __result = false;
                return false;
            }
            if (__instance.m_weapon_level[(int)wt] == WeaponUnlock.LOCKED || (wt == WeaponType.IMPULSE && __instance.m_weapon_level[(int)wt] == WeaponUnlock.LEVEL_1))
            {
                __instance.m_weapon_picked_up[(int)wt] = picked_up;
                if (__instance.WeaponUsesAmmo(wt))
                //if (MPWeapons.WeaponUsesAmmo(MPShips.GetShip(__instance.c_player_ship), wt))
                {
                    __instance.AddAmmo(200, true, false, true);
                }
                else
                {
                    __instance.AddEnergy(10f, true, false);
                }

                if (wt == WeaponType.IMPULSE)
                {
                    __instance.m_weapon_level[(int)wt] = WeaponUnlock.LEVEL_2A;
                    __instance.m_weapon_picked_up[(int)wt] = true;
                    MPAutoSelection.WeaponPickup.UnlockWeaponEvent(WeaponType.IMPULSE, __instance);
                }
                else
                {
                    __instance.m_weapon_level[(int)wt] = ((!GameplayManager.IsChallengeMode && !GameplayManager.IsMultiplayer) ? WeaponUnlock.LEVEL_0 : WeaponUnlock.LEVEL_1);
                }
                __instance.CallRpcUnlockWeaponClient(wt, silent);
                __result = true;
            }
            else
            {
                __result = false;
            }
            
            return false;
        }
    }

    /// <summary>
    /// Replace Player::UnlockWeaponClient() function
    /// </summary>
    [HarmonyPatch(typeof(Player), "UnlockWeaponClient")]
    class MPClassic_Player_UnlockWeaponClient
    {
        static bool Prefix(Player __instance, WeaponType wt, bool silent)
        {
            if (!GameplayManager.IsMultiplayer || !MPClassic.matchEnabled)
                return true;

            if (wt != WeaponType.IMPULSE)
                return true;

            if (!silent && __instance.isLocalPlayer)
            {
                GameplayManager.AddHUDMessage(Loc.LS("IMPULSE+ UPGRADED TO IMPULSE++ Q"), -1, true);
                SFXCueManager.PlayRawSoundEffect2D(SoundEffect.hud_notify_message1, 1f, 0.15f, 0.1f, false);
                __instance.m_weapon_level[(int)wt] = WeaponUnlock.LEVEL_2A;
            }

            if (__instance.isLocalPlayer)
            {
                __instance.MaybeShow3rdWeaponTip();
                __instance.UpdateCurrentWeaponName();
            }

            return false;
        }
    }

    /// <summary>
    /// Remove IsMultiplayerActive override in PlayerShip::MaybeFireWeapon which assumes if multiplayer, force all projectiles to level2a quads
    /// </summary>
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    class MPClassic_PlayerShip_MaybeFireWeapon
    {
        static bool MatchEnabledHelper()
        {
            return GameplayManager.IsMultiplayer && !MPClassic.matchEnabled;
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int state = 0;
            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(GameplayManager), "IsMultiplayerActive"))
                {
                    state++;
                    // Skip IsMultiplayerActive check for impulse
                    if (state == 2)
                    {
                        code.opcode = OpCodes.Call;
                        code.operand = AccessTools.Method(typeof(MPClassic_PlayerShip_MaybeFireWeapon), "MatchEnabledHelper");
                    }
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Force loadouts to Imp/Falc
    /// </summary>
    [HarmonyPatch(typeof(MenuManager), "BuildPrivateMatchData")]
    class MPClassic_MenuManager_BuildPrivateMatchData
    {
        static void Postfix(PrivateMatchDataMessage __result)
        {
            if (!Menus.mms_classic_spawns)
                return;

            __result.m_force_loadout = 1;
            __result.m_force_w1 = WeaponType.IMPULSE;
            __result.m_force_w2 = WeaponType.NUM;
            __result.m_force_m1 = MissileType.FALCON;
            __result.m_force_m2 = MissileType.NUM;
        }
    }

    [HarmonyPatch(typeof(Item), "OnTriggerEnter")]
    class MPClassic_Item_OnTriggerEnter
    {
        static bool flag2(Player player, bool flag, bool flag2, WeaponType wt)
        {
            if (!GameplayManager.IsMultiplayer || !MPClassic.matchEnabled)
                return true;

            if (wt == WeaponType.IMPULSE && player.m_weapon_level[(int)wt] == WeaponUnlock.LEVEL_1)
            {
                return true;
            }
            else
            {
                return true;
            }
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
        {
            int i = 0;

            foreach (var code in codes)
            {
                if (code.opcode == OpCodes.Call && code.operand == AccessTools.Method(typeof(Overload.NetworkManager), "IsServer"))
                {
                    i++;

                    if (i == 2)
                    {
                        yield return new CodeInstruction(OpCodes.Ldloc_1);
                        yield return new CodeInstruction(OpCodes.Ldloc_2);
                        yield return new CodeInstruction(OpCodes.Ldloc_3);
                        yield return new CodeInstruction(OpCodes.Ldloc_S, 5);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPClassic_Item_OnTriggerEnter), "flag2"));
                        yield return new CodeInstruction(OpCodes.Stloc_3);
                    }

                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Despawn spewed primaries after 30 seconds.
    /// </summary>
    [HarmonyPatch(typeof(Item), "MaybeDespawnPowerup")]
    public class MPClassic_Item_MaybeDespawnPowerup
    {
        public static bool Prefix(Item __instance, ref float __result)
        {
            if (MPClassic.matchEnabled && GameplayManager.IsMultiplayerActive && Server.IsActive() && __instance.m_spawn_point == -1 &&
                (bool)AccessTools.Field(typeof(Item), "m_spewed").GetValue(__instance)
            )
            {
                switch (__instance.m_type)
                {
                    case ItemType.WEAPON_IMPULSE:
                    case ItemType.WEAPON_CYCLONE:
                    case ItemType.WEAPON_REFLEX:
                    case ItemType.WEAPON_DRILLER:
                    case ItemType.WEAPON_SHOTGUN:
                    case ItemType.WEAPON_FLAK:
                    case ItemType.WEAPON_THUNDERBOLT:
                    case ItemType.WEAPON_LANCER:
                        //__result = 0.25f;
                        __result = 1f; // let them sit a while
                        return false;
                    default:
                        return true;
                }
            }

            return true;
        }
    }
}
