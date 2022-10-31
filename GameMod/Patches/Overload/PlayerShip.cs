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
        public static void Postfix(PlayerShip __instance) {
            __instance.c_camera_transform.localScale = Vector3.one * VRScale.VR_Scale;
        }
    }

    /// <summary>
    /// Enables the keybind for the previous weapon.
    /// </summary>
    [Mod(Mods.PreviousWeaponFix)]
    [HarmonyPatch(typeof(PlayerShip), "UpdateReadImmediateControls")]
    public class PlayerShip_UpdateReadImmediateControls {
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
