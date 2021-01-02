using Harmony;
using Overload;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.Networking;

namespace GameMod
{

    static class MPClassic
    {
        public const float minRespawnTimerMultiplier = 1f; // Multiplier against default 30s
        public const float maxRespawnTimerMultiplier = 1f; // Multiplier against default 60s

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
    /// Spawn algorithm (currently stock)
    /// </summary>
    [HarmonyPatch(typeof(NetworkMatch), "MaybeSpawnPowerup")]
    class MPClassic_NetworkMatch_MaybeSpawnPowerup
    {
        static bool Prefix(ref int[] ___m_spawn_weapon_count, ref float[] ___m_spawn_weapon_timer, ref float ___m_spawn_basic_timer, ref float ___m_spawn_missile_timer, ref float ___m_spawn_super_timer)
        {
            if (!GameplayManager.IsMultiplayer || !MPClassic.matchEnabled)
                return true;

            for (int i = 0; i < 8; i++)
            {
                if (___m_spawn_weapon_count[i] > 0)
                {
                    ___m_spawn_weapon_timer[i] -= RUtility.FRAMETIME_GAME;
                    if (___m_spawn_weapon_timer[i] <= 0f)
                    {
                        //NetworkMatch.SpawnItem(ChallengeManager.WeaponTypeToPrefab((WeaponType)i), false);
                        AccessTools.Method(typeof(NetworkMatch), "SpawnItem").Invoke(null, new object[] { ChallengeManager.WeaponTypeToPrefab((WeaponType)i), false });
                        ___m_spawn_weapon_count[i]--;
                        if (___m_spawn_weapon_count[i] > 0)
                        {
                            ___m_spawn_weapon_timer[i] = UnityEngine.Random.Range(30f * MPClassic.minRespawnTimerMultiplier, 60f * MPClassic.maxRespawnTimerMultiplier);
                        }
                    }
                }
            }
            if (___m_spawn_basic_timer > 0f)
            {
                ___m_spawn_basic_timer -= RUtility.FRAMETIME_GAME;
                if (___m_spawn_basic_timer <= 0f)
                {
                    int num = UnityEngine.Random.Range(0, 4);
                    if (NetworkMatch.AnyPlayersHaveAmmoWeapons())
                    {
                        num = UnityEngine.Random.Range(0, 5);
                    }
                    ItemPrefab item;
                    switch (num)
                    {
                        case 1:
                        case 2:
                            item = ItemPrefab.entity_item_energy;
                            break;
                        case 3:
                        case 4:
                            item = ItemPrefab.entity_item_ammo;
                            break;
                        default:
                            item = ItemPrefab.entity_item_shields;
                            break;
                    }
                    //NetworkMatch.SpawnItem(item, false);
                    AccessTools.Method(typeof(NetworkMatch), "SpawnItem").Invoke(null, new object[] { item, false });
                    NetworkMatch.SetSpawnBasicTimer();
                }
            }
            if (___m_spawn_missile_timer > 0f)
            {
                ___m_spawn_missile_timer -= RUtility.FRAMETIME_GAME;
                if (___m_spawn_missile_timer <= 0f)
                {
                    MissileType missileType = NetworkMatch.RandomAllowedMissileSpawn();
                    if (missileType != MissileType.NUM)
                    {
                        ItemPrefab item2 = ChallengeManager.MissileTypeToPrefab(missileType);
                        //NetworkMatch.SpawnItem(item2, false);
                        AccessTools.Method(typeof(NetworkMatch), "SpawnItem").Invoke(null, new object[] { item2, false });
                    }
                    //NetworkMatch.UpdateLastMissileCount();
                    AccessTools.Method(typeof(NetworkMatch), "UpdateLastMissileCount").Invoke(null, null);
                    NetworkMatch.SetSpawnMissileTimer();
                }
            }
            if (___m_spawn_super_timer > 0f)
            {
                float spawn_super_timer = ___m_spawn_super_timer;
                ___m_spawn_super_timer -= RUtility.FRAMETIME_GAME;
                if (spawn_super_timer > 10f && ___m_spawn_super_timer <= 10f)
                {
                    GameManager.m_local_player.CallRpcShowWarningMessage(0);
                }
                if (___m_spawn_super_timer <= 0f)
                {
                    if (NetworkMatch.SpawnSuperPowerup())
                    {
                        GameManager.m_local_player.CallRpcShowWarningMessage(1);
                    }
                    NetworkMatch.SetSpawnSuperTimer();
                }
            }

            return false;
        }
    }
}
