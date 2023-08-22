using HarmonyLib;
using Overload;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace GameMod
{
    public class MPWeaponBehavior
    {

        /// <summary>
        /// Thunderbolt Tweaks
        /// </summary>
        internal static class Thunderbolt
        {
            private static int m_charge_loop_index = -1;
            private static float m_tb_overchargedamage_multiplier = 4f; // 4.0dps self-damage instead of stock 1.0dps)
            private static float m_muzzle_adjust = 0.2f; // Projectile exit point offsets

            private static void StopThunderboltSelfDamageLoop()
            {
                if (m_charge_loop_index != -1)
                {
                    GameManager.m_audio.StopSound(m_charge_loop_index);
                    m_charge_loop_index = -1;
                }
            }

            private static float GetThunderboltSelfDamageMultiplier()
            {
                return GameplayManager.IsMultiplayer ? m_tb_overchargedamage_multiplier : 1f;
            }

            // Self damage and charge time
            [HarmonyPatch(typeof(PlayerShip), "ThunderCharge")]
            class MPWeaponBehavior_Thunderbolt_PlayerShip_ThunderCharge
            {

                static void Prefix(PlayerShip __instance)
                {
                    if (__instance.m_refire_time <= 0f && __instance.m_thunder_power == 0f)
                        StopThunderboltSelfDamageLoop();
                }

                // Audio cue once charged
                static void Postfix(PlayerShip __instance)
                {
                    if (__instance.isLocalPlayer && __instance.m_thunder_power >= 2f && m_charge_loop_index == -1)
                    {
                        m_charge_loop_index = GameManager.m_audio.PlayCue2DLoop((int)SoundEffect.cine_sfx_warning_loop, 1f, 0f, 0f, true);
                    }
                }

                // Override self-damage ramping
                static float GetSelfChargeDamage(float num, PlayerShip playerShip)
                {
                    return GetThunderboltSelfDamageMultiplier() * num;
                }

                static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
                {
                    foreach (var code in codes)
                    {
                        // di.damage = ((this.m_thunder_power <= 3f) ? (this.m_thunder_power - 2f) : 1f) * num;
                        if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(DamageInfo), "damage"))
                        {
                            yield return code;
                            yield return new CodeInstruction(OpCodes.Ldloca_S, 2);
                            yield return new CodeInstruction(OpCodes.Ldloc_0);
                            yield return new CodeInstruction(OpCodes.Ldarg_0);
                            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeaponBehavior_Thunderbolt_PlayerShip_ThunderCharge), "GetSelfChargeDamage"));
                            yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(DamageInfo), "damage"));
                            continue;
                        }

                        yield return code;
                    }
                }

                [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
                class MPWeaponBehavior_Thunderbolt_PlayerShip_MaybeFireWeapon
                {

                    private static Vector3 AdjustLeftPos(Vector3 muzzle_pos, Vector3 c_right)
                    {
                        return GameplayManager.IsMultiplayerActive ? muzzle_pos + c_right * m_muzzle_adjust : muzzle_pos;
                    }

                    private static Vector3 AdjustRightPos(Vector3 muzzle_pos, Vector3 c_right)
                    {
                        return GameplayManager.IsMultiplayerActive ? muzzle_pos + c_right * -m_muzzle_adjust : muzzle_pos;
                    }

                    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
                    {
                        int state = 0;
                        foreach (var code in codes)
                        {
                            // case WeaponType.THUNDERBOLT:
                            if (state == 0 && code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 27)
                                state++;

                            // ProjectileManager.PlayerFire(this.c_player, type, this.m_muzzle_right.position, rot, this.m_thunder_power, this.c_player.m_weapon_level[(int)this.c_player.m_weapon_type], true, 0, -1);
                            if (state == 1 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Transform), "get_position"))
                            {
                                state++;
                                yield return code;
                                yield return new CodeInstruction(OpCodes.Ldloc_2);
                                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeaponBehavior_Thunderbolt_PlayerShip_MaybeFireWeapon), "AdjustRightPos"));
                                continue;
                            }

                            // ProjectileManager.PlayerFire(this.c_player, type, this.m_muzzle_left.position, rot, this.m_thunder_power, this.c_player.m_weapon_level[(int)this.c_player.m_weapon_type], false, 1, -1);
                            if (state == 2 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Transform), "get_position"))
                            {
                                state++;
                                yield return code;
                                yield return new CodeInstruction(OpCodes.Ldloc_2);
                                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeaponBehavior_Thunderbolt_PlayerShip_MaybeFireWeapon), "AdjustLeftPos"));
                                continue;
                            }

                            // this.m_thunder_sound_timer = 0f;
                            if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_thunder_sound_timer"))
                            {
                                yield return code;
                                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(MPWeaponBehavior.Thunderbolt), "StopThunderboltSelfDamageLoop"));
                                continue;
                            }

                            yield return code;
                        }
                    }
                }

                [HarmonyPatch(typeof(PlayerShip), "OnDestroy")]
                class MPWeaponBehavior_Thunderbolt_PlayerShip_OnDestroy
                {
                    static void Postfix()
                    {
                        StopThunderboltSelfDamageLoop();
                    }
                }

                [HarmonyPatch(typeof(PlayerShip), "OnDisable")]
                class MPWeaponBehavior_Thunderbolt_PlayerShip_OnDisable
                {
                    static void Postfix()
                    {
                        StopThunderboltSelfDamageLoop();
                    }
                }

                [HarmonyPatch(typeof(PlayerShip), "Update")]
                class MPWeaponBehavior_Thunderbolt_PlayerShip_Update
                {
                    static void Postfix(PlayerShip __instance)
                    {
                        if ((__instance.m_boosting || __instance.m_dead || __instance.m_dying) && GameplayManager.IsMultiplayerActive && __instance.isLocalPlayer)
                        {
                            StopThunderboltSelfDamageLoop();
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cyclone Tweaks
    /// </summary>
    internal static class Cyclone
    {
        private static FieldInfo _PlayerShip_flak_fire_count_Field = typeof(PlayerShip).GetField("flak_fire_count", BindingFlags.NonPublic | BindingFlags.Instance);
        public static int CycloneSpinupStartingStep = 6;

        /// <summary>
        /// Simply adjust flak_fire_count input which would normally start at 0 in existing formula:
        /// 1f - Mathf.Min((float)flak_fire_count * 0.05f, (c_player.m_weapon_level[(int)c_player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.25f)
        /// </summary>
        /// <param name="player_ship"></param>
        /// <returns></returns>
        static float GetCycloneSpinupAdjustment(PlayerShip player_ship)
        {
            int flak_fire_count = (int)_PlayerShip_flak_fire_count_Field.GetValue(player_ship);
            if (flak_fire_count == 0)
            {
                _PlayerShip_flak_fire_count_Field.SetValue(player_ship, CycloneSpinupStartingStep);
            }
            return 1f - Mathf.Min(flak_fire_count * 0.05f, (player_ship.c_player.m_weapon_level[(int)player_ship.c_player.m_weapon_type] != WeaponUnlock.LEVEL_2B) ? 0.4f : 0.25f);
        }

        [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
        internal class MPWeaponBehavior_Cyclone_PlayerShip_MaybeFireWeapon
        {
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes)
            {
                foreach (var code in codes)
                {
                    if (code.opcode == OpCodes.Stloc_S && ((LocalBuilder)code.operand).LocalIndex == 19)
                    {
                        yield return code;
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Cyclone), "GetCycloneSpinupAdjustment"));
                        yield return new CodeInstruction(OpCodes.Stloc_S, 19);
                        continue;
                    }
                    yield return code;
                }
            }
        }
    }
}