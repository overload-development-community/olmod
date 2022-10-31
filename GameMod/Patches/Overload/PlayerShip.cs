using System.Collections.Generic;
using System.Reflection.Emit;
using GameMod.Metadata;
using GameMod.Objects;
using HarmonyLib;
using Overload;
using UnityEngine;

namespace GameMod.Patches.Overload {
    /// <summary>
    /// Reports damage to the tracker.
    /// </summary>
    [Mod(Mods.Tracker)]
    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    public class PlayerShip_ApplyDamage {
        public static bool Prepare() {
            return Config.Settings.Value<bool>("isServer") && !string.IsNullOrEmpty(Config.Settings.Value<string>("trackerBaseUrl"));
        }

        public static void Prefix(DamageInfo di, PlayerShip __instance) {
            if (!NetworkManager.IsHeadless() || di.damage == 0f || __instance.m_death_stats_recorded || __instance.m_cannot_die || __instance.c_player.m_invulnerable)
                return;

            var otherPlayer = di.owner?.GetComponent<Player>();

            float hitpoints = __instance.c_player.m_hitpoints;

            // Increase hitpoints by ratio of damage reduction so that we report the correct value.
            float reduction = Player.ARMOR_DAMAGE[__instance.c_player.m_upgrade_level[0]];
            if (di.type == DamageType.EXPLOSIVE && __instance.c_player.m_unlock_blast_damage) {
                reduction *= 0.8f;
            }
            hitpoints /= reduction;

            ProjPrefab weapon = di.weapon;

            float damage = di.damage;
            if (hitpoints - di.damage <= 0f)
                damage = hitpoints;
            Tracker.AddDamage(__instance.c_player, otherPlayer, weapon, damage);
        }
    }

    /// <summary>
    /// Sets the VR scale upon entering the game.
    /// </summary>
    [Mod(Mods.VRScale)]
    [HarmonyPatch(typeof(PlayerShip), "Awake")]
    public class PlayerShip_Awake {
        public static bool Prepare() {
            return Switches.VREnabled;
        }

        public static void Postfix(PlayerShip __instance) {
            __instance.c_camera_transform.localScale = Vector3.one * VRScale.VR_Scale;
        }
    }

    /// <summary>
    /// Updates where thunderbolt appears from when fired from a ship.
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "MaybeFireWeapon")]
    public class PlayerShip_MaybeFireWeapon {

        public static Vector3 AdjustLeftPos(Vector3 muzzle_pos, Vector3 c_right) {
            return GameplayManager.IsMultiplayerActive ? muzzle_pos + c_right * ThunderboltBalance.m_muzzle_adjust : muzzle_pos;
        }

        public static Vector3 AdjustRightPos(Vector3 muzzle_pos, Vector3 c_right) {
            return GameplayManager.IsMultiplayerActive ? muzzle_pos + c_right * -ThunderboltBalance.m_muzzle_adjust : muzzle_pos;
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            int state = 0;
            foreach (var code in codes) {
                // case WeaponType.THUNDERBOLT:
                if (state == 0 && code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 27)
                    state++;

                // ProjectileManager.PlayerFire(this.c_player, type, this.m_muzzle_right.position, rot, this.m_thunder_power, this.c_player.m_weapon_level[(int)this.c_player.m_weapon_type], true, 0, -1);
                if (state == 1 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Transform), "get_position")) {
                    state++;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerShip_MaybeFireWeapon), "AdjustRightPos"));
                    continue;
                }

                // ProjectileManager.PlayerFire(this.c_player, type, this.m_muzzle_left.position, rot, this.m_thunder_power, this.c_player.m_weapon_level[(int)this.c_player.m_weapon_type], false, 1, -1);
                if (state == 2 && code.opcode == OpCodes.Callvirt && code.operand == AccessTools.Method(typeof(Transform), "get_position")) {
                    state++;
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloc_2);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerShip_MaybeFireWeapon), "AdjustLeftPos"));
                    continue;
                }

                // this.m_thunder_sound_timer = 0f;
                if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(PlayerShip), "m_thunder_sound_timer")) {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThunderboltBalance), "StopThunderboltSelfDamageLoop"));
                    continue;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Stops the overcharge sound when the player ship is destroyed.
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "OnDestroy")]
    public class PlayerShip_OnDestroy {
        public static void Postfix() {
            ThunderboltBalance.StopThunderboltSelfDamageLoop();
        }
    }

    /// <summary>
    /// Stops the overcharge sound when the player ship is disabled.
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "OnDisable")]
    public class PlayerShip_OnDisable {
        public static void Postfix() {
            ThunderboltBalance.StopThunderboltSelfDamageLoop();
        }
    }

    /// <summary>
    /// Self damage and charge time
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "ThunderCharge")]
    public class PlayerShip_ThunderCharge {
        public static void Prefix(PlayerShip __instance) {
            if (__instance.m_refire_time <= 0f && __instance.m_thunder_power == 0f)
                ThunderboltBalance.StopThunderboltSelfDamageLoop();
        }

        // Audio cue once charged
        public static void Postfix(PlayerShip __instance) {
            if (__instance.isLocalPlayer && __instance.m_thunder_power >= 2f && ThunderboltBalance.m_charge_loop_index == -1) {
                ThunderboltBalance.m_charge_loop_index = GameManager.m_audio.PlayCue2DLoop((int)SoundEffect.cine_sfx_warning_loop, 1f, 0f, 0f, true);
            }
        }

        // Override self-damage ramping
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                // di.damage = ((this.m_thunder_power <= 3f) ? (this.m_thunder_power - 2f) : 1f) * num;
                if (code.opcode == OpCodes.Stfld && code.operand == AccessTools.Field(typeof(DamageInfo), "damage")) {
                    yield return code;
                    yield return new CodeInstruction(OpCodes.Ldloca_S, 2);
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ThunderboltBalance), "GetSelfChargeDamage"));
                    yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(DamageInfo), "damage"));
                    continue;
                }

                yield return code;
            }
        }
    }

    /// <summary>
    /// Stops the overcharge sound if it no longer applies.
    /// </summary>
    [Mod(Mods.ThunderboltBalance)]
    [HarmonyPatch(typeof(PlayerShip), "Update")]
    public class PlayerShip_Update {
        public static void Postfix(PlayerShip __instance) {
            if ((__instance.m_boosting || __instance.m_dead || __instance.m_dying) && GameplayManager.IsMultiplayerActive && __instance.isLocalPlayer) {
                ThunderboltBalance.StopThunderboltSelfDamageLoop();
            }
        }
    }

    /// <summary>
    /// Enables the keybind for the previous weapon.
    /// </summary>
    [Mod(Mods.PreviousWeaponFix)]
    [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
    public class PlayerShip_UpdateReadImmediateControls {
        public static bool Prepare() {
            return !GameplayManager.IsDedicatedServer();
        }

        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> codes) {
            foreach (var code in codes) {
                // Before if (Controls.IsPressed(CCInput.SWITCH_MISSILE))
                if (code.opcode == OpCodes.Ldc_I4_S && (sbyte)code.operand == 17) {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(PlayerShip_UpdateReadImmediateControls), "PrevWeaponUpdate"));
                }

                yield return code;
            }
        }

        public static void PrevWeaponUpdate(PlayerShip player) {
            player.c_player.CallCmdSetCurrentWeapon(player.c_player.m_weapon_type);
        }
    }
}
